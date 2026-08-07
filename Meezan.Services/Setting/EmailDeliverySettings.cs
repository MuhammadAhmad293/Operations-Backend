namespace Meezan.Services.Setting
{
    public class EmailDeliverySettings
    {
        public RabbitMqSettings RabbitMq { get; set; } = new();
        public PublisherSettings Publisher { get; set; } = new();
        public ResilienceSettings Resilience { get; set; } = new();
    }

    public class RabbitMqSettings
    {
        public string Host { get; set; } = "localhost";
        public string VirtualHost { get; set; } = "/";
        public string Username { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public int Port { get; set; } = 5672;
        public string Exchange { get; set; } = "email.exchange";
        public string MainQueue { get; set; } = "email.send";
        public string RetryQueue { get; set; } = "email.retry";
        public string DeadLetterQueue { get; set; } = "email.deadletter";
        public int RetryTtlMs { get; set; } = 30000;
    }

    public class PublisherSettings
    {
        public int PollIntervalMs { get; set; } = 500;
        public int BatchSize { get; set; } = 100;
        public int StartupRecoveryMs { get; set; } = 5000;
    }

    public class ResilienceSettings
    {
        public int MaxRetryAttempts { get; set; } = 3;
        public int RetryDelaySeconds { get; set; } = 5;
        public double FailureRatioThreshold { get; set; } = 0.5;
        public int MinimumThroughput { get; set; } = 10;
        public int SamplingDurationSeconds { get; set; } = 30;
        public int BreakDurationSeconds { get; set; } = 60;
    }
}
