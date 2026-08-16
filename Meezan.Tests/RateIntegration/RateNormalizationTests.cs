using System.Net;
using Meezan.IServices.IService;
using Meezan.Services.RateProviders;
using Meezan.Services.Setting;
using Meezan.Tests.TestSupport;

namespace Meezan.Tests.RateIntegration
{
    // meezan-backend.md §7.6 (part 1): metal-price normalization (per-troy-ounce -> per-gram,
    // ÷31.1034768) and the XAU->GOLD / XAG->SILVER code translation boundary, for both providers.
    // No real network call — HttpClient is backed by FakeHttpMessageHandler.
    public class RateNormalizationTests
    {
        private const decimal TroyOunceToGrams = 31.1034768m;

        [Fact]
        public async Task Frankfurter_NormalizesXauToGoldPerGram_AndTranslatesTheCurrencyCode()
        {
            FakeHttpMessageHandler handler = new();
            handler.EnqueueJson(HttpStatusCode.OK, "[{\"date\":\"2026-08-09\",\"base\":\"XAU\",\"quote\":\"USD\",\"rate\":4343.29}]");
            HttpClient httpClient = new(handler);
            FrankfurterRateProvider provider = new(httpClient, new RateIntegrationSettings());

            RateQuote quote = await provider.GetMetalRateAsync("XAU");

            Assert.Equal("GOLD", quote.FromCurrencyCode);
            Assert.Equal("USD", quote.ToCurrencyCode);
            Assert.Equal(4343.29m, quote.RatePerUnit);
            Assert.Equal(4343.29m / TroyOunceToGrams, quote.RatePerGram);
        }

        [Fact]
        public async Task Frankfurter_NormalizesXagToSilverPerGram()
        {
            FakeHttpMessageHandler handler = new();
            handler.EnqueueJson(HttpStatusCode.OK, "[{\"date\":\"2026-08-09\",\"base\":\"XAG\",\"quote\":\"USD\",\"rate\":38.50}]");
            HttpClient httpClient = new(handler);
            FrankfurterRateProvider provider = new(httpClient, new RateIntegrationSettings());

            RateQuote quote = await provider.GetMetalRateAsync("XAG");

            Assert.Equal("SILVER", quote.FromCurrencyCode);
            Assert.Equal(38.50m / TroyOunceToGrams, quote.RatePerGram);
        }

        [Fact]
        public async Task Frankfurter_FiatQuote_HasNoPerGramRate_OnlyPerUnit()
        {
            FakeHttpMessageHandler handler = new();
            handler.EnqueueJson(HttpStatusCode.OK, "[{\"date\":\"2026-08-09\",\"base\":\"USD\",\"quote\":\"EGP\",\"rate\":49.78}]");
            HttpClient httpClient = new(handler);
            FrankfurterRateProvider provider = new(httpClient, new RateIntegrationSettings());

            List<RateQuote> quotes = await provider.GetFiatRatesAsync(new List<string> { "EGP" });

            RateQuote quote = Assert.Single(quotes);
            Assert.Equal("USD", quote.FromCurrencyCode);
            Assert.Equal("EGP", quote.ToCurrencyCode);
            Assert.Equal(49.78m, quote.RatePerUnit);
            Assert.Null(quote.RatePerGram);
        }

        [Fact]
        public async Task GoldApi_NormalizesXauToGoldPerGram_AndTranslatesTheCurrencyCode()
        {
            FakeHttpMessageHandler handler = new();
            handler.EnqueueJson(HttpStatusCode.OK, "{\"currency\":\"USD\",\"exchangeRate\":1.0,\"name\":\"Gold\",\"price\":4343.29,\"symbol\":\"XAU\",\"updatedAt\":\"2026-08-09T00:00:00Z\"}");
            HttpClient httpClient = new(handler);
            GoldApiRateProvider provider = new(httpClient, new RateIntegrationSettings());

            RateQuote quote = await provider.GetMetalRateAsync("XAU");

            Assert.Equal("GOLD", quote.FromCurrencyCode);
            Assert.Equal("USD", quote.ToCurrencyCode);
            Assert.Equal(4343.29m, quote.RatePerUnit);
            Assert.Equal(4343.29m / TroyOunceToGrams, quote.RatePerGram);
        }

        [Fact]
        public async Task GoldApi_GetFiatRatesAsync_IsUnsupported_ItIsAMetalsOnlyFallback()
        {
            HttpClient httpClient = new(new FakeHttpMessageHandler());
            GoldApiRateProvider provider = new(httpClient, new RateIntegrationSettings());

            await Assert.ThrowsAsync<NotSupportedException>(() => provider.GetFiatRatesAsync(new List<string> { "EGP" }));
        }
    }
}
