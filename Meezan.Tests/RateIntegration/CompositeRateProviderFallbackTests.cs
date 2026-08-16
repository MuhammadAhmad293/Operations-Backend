using System.Net;
using Meezan.IServices.IService;
using Meezan.Services.RateProviders;
using Meezan.Services.Setting;
using Meezan.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Meezan.Tests.RateIntegration
{
    // meezan-backend.md §7.6 (part 3): CompositeRateProvider's fallback path — a failing
    // Frankfurter metal quote falls back to gold-api.com. No fiat fallback exists (gold-api.com
    // has no FX endpoint), matching CompositeRateProvider.GetFiatRatesAsync's own comment.
    public class CompositeRateProviderFallbackTests
    {
        private static RateIntegrationSettings FastFailSettings() => new()
        {
            Resilience = new ResilienceSettings
            {
                MaxRetryAttempts = 1, // Polly's minimum valid value
                RetryDelaySeconds = 0, // 0 keeps the one retry effectively instant
                FailureRatioThreshold = 0.5,
                MinimumThroughput = 2,
                SamplingDurationSeconds = 10,
                BreakDurationSeconds = 30,
            },
        };

        [Fact]
        public async Task GetMetalRateAsync_FallsBackToGoldApi_WhenFrankfurterFails()
        {
            RateIntegrationSettings settings = FastFailSettings();

            FakeHttpMessageHandler frankfurterHandler = new();
            frankfurterHandler.EnqueueFailure(HttpStatusCode.ServiceUnavailable);
            frankfurterHandler.EnqueueFailure(HttpStatusCode.ServiceUnavailable); // initial attempt + 1 retry
            FrankfurterRateProvider frankfurter = new(new HttpClient(frankfurterHandler), settings);

            FakeHttpMessageHandler goldApiHandler = new();
            goldApiHandler.EnqueueJson(HttpStatusCode.OK, "{\"currency\":\"USD\",\"exchangeRate\":1.0,\"name\":\"Gold\",\"price\":4343.29,\"symbol\":\"XAU\",\"updatedAt\":\"2026-08-09T00:00:00Z\"}");
            GoldApiRateProvider goldApi = new(new HttpClient(goldApiHandler), settings);

            CompositeRateProvider composite = new(frankfurter, goldApi, settings, NullLoggerFactory.Instance);

            RateQuote quote = await composite.GetMetalRateAsync("XAU");

            Assert.Equal("GoldApi", quote.Source);
            Assert.Equal("GOLD", quote.FromCurrencyCode);
            Assert.Equal(4343.29m, quote.RatePerUnit);
            Assert.Equal(2, frankfurterHandler.RequestCount); // initial attempt + 1 retry, both failed
            Assert.Equal(1, goldApiHandler.RequestCount); // fallback succeeded first try
        }

        [Fact]
        public async Task GetMetalRateAsync_UsesFrankfurter_WhenItSucceeds_NeverCallingGoldApi()
        {
            RateIntegrationSettings settings = FastFailSettings();

            FakeHttpMessageHandler frankfurterHandler = new();
            frankfurterHandler.EnqueueJson(HttpStatusCode.OK, "[{\"date\":\"2026-08-09\",\"base\":\"XAU\",\"quote\":\"USD\",\"rate\":4343.29}]");
            FrankfurterRateProvider frankfurter = new(new HttpClient(frankfurterHandler), settings);

            FakeHttpMessageHandler goldApiHandler = new();
            GoldApiRateProvider goldApi = new(new HttpClient(goldApiHandler), settings);

            CompositeRateProvider composite = new(frankfurter, goldApi, settings, NullLoggerFactory.Instance);

            RateQuote quote = await composite.GetMetalRateAsync("XAU");

            Assert.Equal("Frankfurter", quote.Source);
            Assert.Equal(0, goldApiHandler.RequestCount); // fallback never triggered
        }

        [Fact]
        public async Task GetFiatRatesAsync_PropagatesTheFailure_WithNoFallbackAvailable()
        {
            RateIntegrationSettings settings = FastFailSettings();

            FakeHttpMessageHandler frankfurterHandler = new();
            frankfurterHandler.EnqueueFailure(HttpStatusCode.ServiceUnavailable);
            frankfurterHandler.EnqueueFailure(HttpStatusCode.ServiceUnavailable); // initial attempt + 1 retry
            FrankfurterRateProvider frankfurter = new(new HttpClient(frankfurterHandler), settings);

            GoldApiRateProvider goldApi = new(new HttpClient(new FakeHttpMessageHandler()), settings);
            CompositeRateProvider composite = new(frankfurter, goldApi, settings, NullLoggerFactory.Instance);

            await Assert.ThrowsAsync<HttpRequestException>(() => composite.GetFiatRatesAsync(new List<string> { "EGP" }));
        }
    }
}
