using Meezan.Services.Setting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace Meezan.Services.RateProviders
{
    // Same shape as EmailResiliencePipeline (spec §7) — retry + circuit breaker around a rate
    // provider call. Unlike the email pipeline, callers here need the fetched value back, so
    // ExecuteAsync is generic rather than void.
    public class RateResiliencePipeline
    {
        private readonly ResiliencePipeline _pipeline;
        private readonly ILogger<RateResiliencePipeline> _logger;

        public RateResiliencePipeline(RateIntegrationSettings settings, ILogger<RateResiliencePipeline> logger)
        {
            _logger = logger;
            ResilienceSettings cfg = settings.Resilience;

            _pipeline = new ResiliencePipelineBuilder()
                .AddRetry(new Polly.Retry.RetryStrategyOptions
                {
                    MaxRetryAttempts = cfg.MaxRetryAttempts,
                    Delay = TimeSpan.FromSeconds(cfg.RetryDelaySeconds),
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(IsTransientFailure),
                    OnRetry = args =>
                    {
                        _logger.LogWarning("Rate provider retry attempt {Attempt} after {Delay}ms",
                            args.AttemptNumber + 1, args.RetryDelay.TotalMilliseconds);
                        return ValueTask.CompletedTask;
                    },
                })
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    FailureRatio = cfg.FailureRatioThreshold,
                    MinimumThroughput = cfg.MinimumThroughput,
                    SamplingDuration = TimeSpan.FromSeconds(cfg.SamplingDurationSeconds),
                    BreakDuration = TimeSpan.FromSeconds(cfg.BreakDurationSeconds),
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(IsTransientFailure),
                    OnOpened = args =>
                    {
                        _logger.LogError("Rate provider circuit breaker OPENED: {Reason} — will stay open for {Break}s",
                            args.Outcome.Exception?.Message, cfg.BreakDurationSeconds);
                        return ValueTask.CompletedTask;
                    },
                    OnClosed = args =>
                    {
                        _logger.LogInformation("Rate provider circuit breaker CLOSED — fetching resumed");
                        return ValueTask.CompletedTask;
                    },
                })
                .Build();
        }

        public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
            => await _pipeline.ExecuteAsync(async ct => await action(ct), cancellationToken);

        public bool IsCircuitOpen()
        {
            try
            {
                _pipeline.Execute(_ => { });
                return false;
            }
            catch (BrokenCircuitException)
            {
                return true;
            }
        }

        private static bool IsTransientFailure(Exception ex) => ex switch
        {
            HttpRequestException => true,
            TaskCanceledException => true,
            TimeoutException => true,
            OperationCanceledException => false,
            _ => false,
        };
    }
}
