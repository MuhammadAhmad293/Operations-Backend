using Microsoft.Extensions.Logging;
using Meezan.Services.Setting;
using Polly;
using Polly.CircuitBreaker;
using System.Net.Mail;
using System.Net.Sockets;

namespace Meezan.Services.Email
{
    public class EmailResiliencePipeline
    {
        private readonly ResiliencePipeline _pipeline;
        private readonly ILogger<EmailResiliencePipeline> _logger;

        public EmailResiliencePipeline(EmailDeliverySettings settings, ILogger<EmailResiliencePipeline> logger)
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
                        _logger.LogWarning("SMTP retry attempt {Attempt} after {Delay}ms",
                            args.AttemptNumber + 1, args.RetryDelay.TotalMilliseconds);
                        return ValueTask.CompletedTask;
                    },
                })
                .AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions
                {
                    FailureRatio = cfg.FailureRatioThreshold,
                    MinimumThroughput = cfg.MinimumThroughput,
                    SamplingDuration = TimeSpan.FromSeconds(cfg.SamplingDurationSeconds),
                    BreakDuration = TimeSpan.FromSeconds(cfg.BreakDurationSeconds),
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(IsTransientFailure),
                    OnOpened = args =>
                    {
                        _logger.LogError("Circuit breaker OPENED: {Reason} — will stay open for {Break}s",
                            args.Outcome.Exception?.Message, cfg.BreakDurationSeconds);
                        return ValueTask.CompletedTask;
                    },
                    OnClosed = args =>
                    {
                        _logger.LogInformation("Circuit breaker CLOSED — SMTP delivery resumed");
                        return ValueTask.CompletedTask;
                    },
                })
                .Build();
        }

        public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
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
            SmtpException smtp when (int)smtp.StatusCode >= 400 && (int)smtp.StatusCode < 500 => true,
            SocketException => true,
            IOException => true,
            TimeoutException => true,
            OperationCanceledException => false,
            _ => false,
        };
    }
}