using Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;
using Meezan.IRepositories.UnitOfWork;
using Meezan.Services.Messaging;
using Meezan.Services.Metrics;
using Meezan.Services.Setting;
using Polly.CircuitBreaker;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Meezan.Services.Email
{
    public class EmailConsumer : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly RabbitConnectionManager _connectionManager;
        private readonly IRabbitPublisher _publisher;
        private readonly EmailResiliencePipeline _resilience;
        private readonly EmailDeliverySettings _settings;
        private readonly EmailMetrics _metrics;
        private readonly ILogger<EmailConsumer> _logger;
        private IChannel? _channel;

        public EmailConsumer(
            IServiceScopeFactory scopeFactory,
            RabbitConnectionManager connectionManager,
            IRabbitPublisher publisher,
            EmailResiliencePipeline resilience,
            EmailDeliverySettings settings,
            EmailMetrics metrics,
            ILogger<EmailConsumer> logger)
        {
            _scopeFactory = scopeFactory;
            _connectionManager = connectionManager;
            _publisher = publisher;
            _resilience = resilience;
            _settings = settings;
            _metrics = metrics;
            _logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
            => base.StartAsync(cancellationToken);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait until RabbitMQ connection is available before starting to consume
            while (!stoppingToken.IsCancellationRequested && !_connectionManager.IsConnected)
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

            if (stoppingToken.IsCancellationRequested)
                return;

            _channel = await _connectionManager.GetConnection().CreateChannelAsync(cancellationToken: stoppingToken);
            await RabbitTopologyDeclarator.DeclareAsync(_channel, _settings.RabbitMq, stoppingToken);
            await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

            AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, ea) => await HandleMessageAsync(ea, stoppingToken);

            await _channel.BasicConsumeAsync(
                queue: _settings.RabbitMq.MainQueue,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }

        private async Task HandleMessageAsync(BasicDeliverEventArgs ea, CancellationToken ct)
        {
            Stopwatch sw = Stopwatch.StartNew();
            string body = Encoding.UTF8.GetString(ea.Body.Span);
            JsonElement envelope;
            string messageId;
            int mailId;

            try
            {
                envelope = JsonSerializer.Deserialize<JsonElement>(body);
                messageId = envelope.GetProperty("MessageId").GetString()!;
                mailId = envelope.GetProperty("MailId").GetInt32();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse message envelope — discarding");
                await AckAsync(ea.DeliveryTag, ct);
                return;
            }

            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            IUnitOfWork uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            ISmtpEmailSender smtpSender = scope.ServiceProvider.GetRequiredService<ISmtpEmailSender>();

            // Inbox deduplication
            if (await uow.ProcessedMessageRepository.ExistsAsync(messageId, ct))
            {
                _logger.LogInformation("Duplicate message skipped: MessageId={MessageId}", messageId);
                await AckAsync(ea.DeliveryTag, ct);
                return;
            }

            Mail? mail = await uow.MailRepository.GetByIdAsync(mailId, ct);
            if (mail is null)
            {
                _logger.LogWarning("Mail not found: MailId={MailId} — discarding", mailId);
                await AckAsync(ea.DeliveryTag, ct);
                return;
            }

            mail.DeliveryStatus = DeliveryStatus.Processing;
            mail.LastAttemptAt = DateTime.UtcNow;
            uow.MailRepository.Update(mail);
            await uow.CommitAsync(ct);

            try
            {
                // Circuit open — do NOT call SMTP; reroute to retry queue
                if (_resilience.IsCircuitOpen())
                {
                    mail.LastError = "Circuit open — rerouted to retry queue";
                    mail.DeliveryStatus = DeliveryStatus.Retrying;
                    uow.MailRepository.Update(mail);
                    await uow.CommitAsync(ct);

                    await RepublishToRetryQueueAsync(ea.Body.ToArray(), ea.BasicProperties, ct);
                    await AckAsync(ea.DeliveryTag, ct);

                    _logger.LogWarning("Circuit open: MailId={MailId} MessageId={MessageId} — rerouted to retry queue",
                        mailId, messageId);
                    return;
                }

                Stopwatch smtpSw = Stopwatch.StartNew();
                await _resilience.ExecuteAsync(token => smtpSender.SendAsync(mail, token), ct);
                _metrics.SmtpDuration.Record(smtpSw.Elapsed.TotalMilliseconds);

                // Success
                mail.MailStatusId = (int)MailStatusEnum.Sent;
                mail.SentAt = DateTime.UtcNow;
                uow.MailRepository.Update(mail);

                uow.ProcessedMessageRepository.Create(new ProcessedMessage
                {
                    MessageId = messageId,
                    ProcessedAt = DateTime.UtcNow,
                });

                await uow.CommitAsync(ct);
                await AckAsync(ea.DeliveryTag, ct);
                _metrics.EmailsSent.Add(1);

                _logger.LogInformation(
                    "Email sent: MailId={MailId} MessageId={MessageId} ElapsedMs={ElapsedMs}",
                    mailId, messageId, sw.ElapsedMilliseconds);
            }
            catch (BrokenCircuitException ex)
            {
                mail.LastError = $"Circuit open: {ex.Message}";
                mail.DeliveryStatus = DeliveryStatus.Retrying;
                uow.MailRepository.Update(mail);
                await uow.CommitAsync(ct);

                await RepublishToRetryQueueAsync(ea.Body.ToArray(), ea.BasicProperties, ct);
                await AckAsync(ea.DeliveryTag, ct);
                _metrics.CircuitOpenCount.Add(1);
                _metrics.EmailsRetry.Add(1);

                _logger.LogWarning("Circuit open caught: MailId={MailId} MessageId={MessageId}", mailId, messageId);
            }
            catch (Exception ex) when (IsPermanentFailure(ex))
            {
                mail.MailStatusId = (int)MailStatusEnum.Failed;
                mail.DeliveryStatus = DeliveryStatus.DeadLetter;
                mail.LastError = ex.Message;
                uow.MailRepository.Update(mail);
                await uow.CommitAsync(ct);

                await NackAsync(ea.DeliveryTag, requeue: false, ct);
                _metrics.EmailsFailed.Add(1);
                _metrics.EmailsDeadLetter.Add(1);

                _logger.LogError(ex, "Permanent SMTP failure: MailId={MailId} MessageId={MessageId}", mailId, messageId);
            }
            catch (Exception ex)
            {
                mail.DeliveryStatus = DeliveryStatus.Retrying;
                mail.RetryCount++;
                mail.LastError = ex.Message;
                uow.MailRepository.Update(mail);
                await uow.CommitAsync(ct);

                await NackAsync(ea.DeliveryTag, requeue: false, ct);
                _metrics.EmailsRetry.Add(1);

                _logger.LogWarning(ex,
                    "Transient SMTP failure: MailId={MailId} MessageId={MessageId} RetryCount={RetryCount}",
                    mailId, messageId, mail.RetryCount);
            }
        }

        private async Task RepublishToRetryQueueAsync(byte[] body, IReadOnlyBasicProperties originalProps, CancellationToken ct)
        {
            BasicProperties retryProps = new BasicProperties
            {
                Persistent = true,
                MessageId = originalProps.MessageId,
            };
            await _publisher.PublishAsync(_settings.RabbitMq.Exchange, _settings.RabbitMq.RetryQueue, body, retryProps, ct);
        }

        private async Task AckAsync(ulong deliveryTag, CancellationToken ct)
        {
            if (_channel is not null)
                await _channel.BasicAckAsync(deliveryTag, multiple: false, ct);
        }

        private async Task NackAsync(ulong deliveryTag, bool requeue, CancellationToken ct)
        {
            if (_channel is not null)
                await _channel.BasicNackAsync(deliveryTag, multiple: false, requeue: requeue, ct);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);
            if (_channel is not null)
                await _channel.CloseAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _channel?.Dispose();
            base.Dispose();
        }

        private static bool IsPermanentFailure(Exception ex) => ex switch
        {
            System.Net.Mail.SmtpException smtp when (int)smtp.StatusCode >= 500 => true,
            System.Security.Authentication.AuthenticationException => true,
            _ => false,
        };
    }
}