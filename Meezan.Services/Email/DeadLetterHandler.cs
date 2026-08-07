using Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;
using Meezan.IRepositories.UnitOfWork;
using Meezan.Services.Messaging;
using Meezan.Services.Setting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Meezan.Services.Email
{
    public class DeadLetterHandler : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly RabbitConnectionManager _connectionManager;
        private readonly EmailDeliverySettings _settings;
        private readonly ILogger<DeadLetterHandler> _logger;
        private IChannel? _channel;

        public DeadLetterHandler(
            IServiceScopeFactory scopeFactory,
            RabbitConnectionManager connectionManager,
            EmailDeliverySettings settings,
            ILogger<DeadLetterHandler> logger)
        {
            _scopeFactory = scopeFactory;
            _connectionManager = connectionManager;
            _settings = settings;
            _logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
            => base.StartAsync(cancellationToken);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested && !_connectionManager.IsConnected)
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

            if (stoppingToken.IsCancellationRequested)
                return;

            _channel = await _connectionManager.GetConnection().CreateChannelAsync(cancellationToken: stoppingToken);
            await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

            AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, ea) => await HandleDeadLetterAsync(ea, stoppingToken);

            await _channel.BasicConsumeAsync(
                queue: _settings.RabbitMq.DeadLetterQueue,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }

        private async Task HandleDeadLetterAsync(BasicDeliverEventArgs ea, CancellationToken ct)
        {
            string body = Encoding.UTF8.GetString(ea.Body.Span);
            string messageId = "unknown";
            int mailId = 0;

            try
            {
                JsonElement envelope = JsonSerializer.Deserialize<JsonElement>(body);
                messageId = envelope.GetProperty("MessageId").GetString()!;
                mailId = envelope.GetProperty("MailId").GetInt32();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse dead-letter envelope — discarding");
                await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
                return;
            }

            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            IUnitOfWork uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            Mail? mail = await uow.MailRepository.GetByIdAsync(mailId, ct);
            if (mail is not null)
            {
                mail.MailStatusId = (int)MailStatusEnum.Failed;
                mail.DeliveryStatus = DeliveryStatus.DeadLetter;
                uow.MailRepository.Update(mail);
                await uow.CommitAsync(ct);
            }

            _logger.LogError(
                "Dead-lettered: MailId={MailId} MessageId={MessageId} RetryCount={RetryCount} LastError={LastError}",
                mailId, messageId, mail?.RetryCount, mail?.LastError);

            await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
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
    }
}