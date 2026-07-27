using System.Diagnostics.Metrics;

namespace Operations.Services.Metrics
{
    public class EmailMetrics : IDisposable
    {
        public const string MeterName = "Operations.Email";

        private readonly Meter _meter;

        public Counter<long> EmailsSent { get; }
        public Counter<long> EmailsFailed { get; }
        public Counter<long> EmailsRetry { get; }
        public Counter<long> EmailsDeadLetter { get; }
        public Histogram<double> RabbitPublishDuration { get; }
        public Histogram<double> SmtpDuration { get; }
        public Counter<long> CircuitOpenCount { get; }

        public EmailMetrics(IMeterFactory meterFactory)
        {
            _meter = meterFactory.Create(MeterName);

            EmailsSent = _meter.CreateCounter<long>("emails.sent", "emails", "Total emails successfully sent via SMTP");
            EmailsFailed = _meter.CreateCounter<long>("emails.failed", "emails", "Total emails that permanently failed");
            EmailsRetry = _meter.CreateCounter<long>("emails.retry", "emails", "Total emails routed to retry queue");
            EmailsDeadLetter = _meter.CreateCounter<long>("emails.deadletter", "emails", "Total emails in dead-letter queue");
            RabbitPublishDuration = _meter.CreateHistogram<double>("rabbit.publish.duration", "ms", "Time to publish a message to RabbitMQ (including broker confirm)");
            SmtpDuration = _meter.CreateHistogram<double>("smtp.duration", "ms", "Time to deliver a message via SMTP");
            CircuitOpenCount = _meter.CreateCounter<long>("circuit.open.count", "events", "Number of times circuit breaker opened");
        }

        public void Dispose() => _meter.Dispose();
    }
}