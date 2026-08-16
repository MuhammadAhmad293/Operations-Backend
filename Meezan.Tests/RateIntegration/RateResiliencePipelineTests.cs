using Meezan.Services.RateProviders;
using Meezan.Services.Setting;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.CircuitBreaker;

namespace Meezan.Tests.RateIntegration
{
    // meezan-backend.md §7.6 (part 2): circuit-breaker-open behavior. No HTTP involved here —
    // RateResiliencePipeline.ExecuteAsync wraps any async delegate, so the breaker itself is
    // tested directly against failing/succeeding delegates rather than through HTTP.
    public class RateResiliencePipelineTests
    {
        private static RateResiliencePipeline CreatePipeline()
        {
            RateIntegrationSettings settings = new()
            {
                Resilience = new ResilienceSettings
                {
                    MaxRetryAttempts = 1, // Polly's minimum valid value; retry is the outer
                    RetryDelaySeconds = 0, // strategy, so each failing ExecuteAsync call can
                    FailureRatioThreshold = 0.5, // reach the circuit breaker (the inner strategy)
                    MinimumThroughput = 4, // up to twice — the counts below use enough margin
                    SamplingDurationSeconds = 10, // that this doesn't matter either way.
                    BreakDurationSeconds = 30,
                },
            };
            return new RateResiliencePipeline(settings, NullLogger<RateResiliencePipeline>.Instance);
        }

        [Fact]
        public async Task ExecuteAsync_ReturnsTheActionsResult_WhenItSucceeds()
        {
            RateResiliencePipeline pipeline = CreatePipeline();

            int result = await pipeline.ExecuteAsync(_ => Task.FromResult(42));

            Assert.Equal(42, result);
        }

        [Fact]
        public async Task IsCircuitOpen_BecomesTrue_OnceEnoughCallsFailToExceedTheRatioThreshold()
        {
            RateResiliencePipeline pipeline = CreatePipeline();

            Assert.False(pipeline.IsCircuitOpen());

            for (int i = 0; i < 4; i++)
            {
                try
                {
                    await pipeline.ExecuteAsync<int>(_ => throw new HttpRequestException("simulated transient failure"));
                }
                catch (Exception ex) when (ex is HttpRequestException or BrokenCircuitException)
                {
                    // expected on the way to (and possibly after) tripping the breaker
                }
            }

            Assert.True(pipeline.IsCircuitOpen());
        }

        [Fact]
        public async Task ExecuteAsync_FailsFastWithoutInvokingTheAction_OnceTheCircuitIsOpen()
        {
            RateResiliencePipeline pipeline = CreatePipeline();
            for (int i = 0; i < 4; i++)
            {
                try
                {
                    await pipeline.ExecuteAsync<int>(_ => throw new HttpRequestException("simulated transient failure"));
                }
                catch (Exception ex) when (ex is HttpRequestException or BrokenCircuitException) { }
            }
            Assert.True(pipeline.IsCircuitOpen());

            int invocationCount = 0;
            await Assert.ThrowsAsync<BrokenCircuitException>(() => pipeline.ExecuteAsync<int>(_ =>
            {
                invocationCount++;
                return Task.FromResult(1);
            }));

            Assert.Equal(0, invocationCount); // the action itself must never run once open
        }

        [Fact]
        public async Task ExecuteAsync_DoesNotTripTheBreaker_WhenFailuresStayBelowTheRatioThreshold()
        {
            RateResiliencePipeline pipeline = CreatePipeline();

            try { await pipeline.ExecuteAsync<int>(_ => throw new HttpRequestException("one blip")); }
            catch (HttpRequestException) { }

            // A run of successes afterward keeps the failure ratio comfortably under 50%
            // regardless of how many inner attempts the one failure above contributed.
            for (int i = 0; i < 10; i++)
                await pipeline.ExecuteAsync(_ => Task.FromResult(1));

            Assert.False(pipeline.IsCircuitOpen());
        }
    }
}
