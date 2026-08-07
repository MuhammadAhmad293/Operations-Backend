using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;
using Meezan.IRepositories.UnitOfWork;
using Meezan.Services.Messaging;
using Meezan.Services.Metrics;
using Meezan.Services.Setting;
using RabbitMQ.Client;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Meezan.Services.Outbox
{
    public class OutboxPublisherService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IRabbitPublisher _publisher;
        private readonly EmailDeliverySettings _settings;
        private readonly EmailMetrics _metrics;
        private readonly ILogger<OutboxPublisherService> _logger;

        public OutboxPublisherService(
            IServiceScopeFactory scopeFactory,
            IRabbitPublisher publisher,
            EmailDeliverySettings settings,
            EmailMetrics metrics,
            ILogger<OutboxPublisherService> logger)
        {
            _scopeFactory = scopeFactory;
            _publisher = publisher;
            _settings = settings;
            _metrics = metrics;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait for RabbitMQ to become available before first poll
            while (!stoppingToken.IsCancellationRequested && !_publisher.IsReady)
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

            if (!stoppingToken.IsCancellationRequested)
                await ResetStuckPublishingAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_publisher.IsReady)
                        await ProcessBatchAsync(stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "OutboxPublisherService encountered an error during batch processing");
                }

                await Task.Delay(_settings.Publisher.PollIntervalMs, stoppingToken);
            }
        }

        private async Task ResetStuckPublishingAsync(CancellationToken ct)
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            IUnitOfWork uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await uow.OutboxRepository.ResetStuckPublishingAsync(ct);
            await uow.CommitAsync(ct);
            _logger.LogInformation("OutboxPublisherService: reset stuck Publishing records on startup");
        }

        private async Task ProcessBatchAsync(CancellationToken ct)
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            IUnitOfWork uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            List<OutboxMessage> batch = await uow.OutboxRepository.GetPendingBatchAsync(_settings.Publisher.BatchSize, ct);
            if (batch.Count == 0)
                return;

            Stopwatch sw = Stopwatch.StartNew();

            // Mark entire batch as Publishing before any publish attempt
            foreach (OutboxMessage outbox in batch)
            {
                outbox.Status = OutboxStatus.Publishing;
                outbox.LastAttempt = DateTime.UtcNow;
                uow.OutboxRepository.Update(outbox);
            }
            await uow.CommitAsync(ct);

            // Publish each message; collect results
            foreach (OutboxMessage outbox in batch)
            {
                string messageId = Guid.NewGuid().ToString();
                try
                {
                    byte[] body = BuildEnvelope(messageId, outbox.MailId);

                    BasicProperties props = new BasicProperties
                    {
                        Persistent = true,
                        MessageId = messageId,
                    };

                    Stopwatch publishSw = Stopwatch.StartNew();
                    await _publisher.PublishAsync(_settings.RabbitMq.Exchange, _settings.RabbitMq.MainQueue, body, props, ct);
                    _metrics.RabbitPublishDuration.Record(publishSw.Elapsed.TotalMilliseconds);

                    outbox.Status = OutboxStatus.Published;
                    outbox.PublishedAt = DateTime.UtcNow;
                    if (outbox.Mail is not null)
                        outbox.Mail.DeliveryStatus = DeliveryStatus.Queued;

                    _logger.LogInformation(
                        "Outbox published: MailId={MailId} MessageId={MessageId}",
                        outbox.MailId, messageId);
                }
                catch (Exception ex)
                {
                    outbox.Status = OutboxStatus.Pending;
                    outbox.RetryCount++;
                    outbox.LastError = ex.Message;

                    _logger.LogWarning(ex,
                        "Outbox publish failed: MailId={MailId} RetryCount={RetryCount}",
                        outbox.MailId, outbox.RetryCount);
                }

                uow.OutboxRepository.Update(outbox);
                if (outbox.Mail is not null)
                    uow.MailRepository.Update(outbox.Mail);
            }

            await uow.CommitAsync(ct);

            _logger.LogInformation(
                "Outbox batch done: BatchSize={BatchSize} ElapsedMs={ElapsedMs}",
                batch.Count, sw.ElapsedMilliseconds);
        }

        private static byte[] BuildEnvelope(string messageId, int mailId)
        {
            var envelope = new { MessageId = messageId, MailId = mailId, OccurredAt = DateTime.UtcNow };
            return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope));
        }
    }
}