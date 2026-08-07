using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Meezan.Services.Messaging
{
    public class RabbitPublisher : IRabbitPublisher
    {
        private readonly RabbitConnectionManager _connectionManager;
        private readonly ILogger<RabbitPublisher> _logger;

        public RabbitPublisher(RabbitConnectionManager connectionManager, ILogger<RabbitPublisher> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        public bool IsReady => _connectionManager.IsConnected;

        public async Task PublishAsync(string exchange, string routingKey, byte[] body, BasicProperties? properties = null, CancellationToken cancellationToken = default)
        {
            IConnection connection = _connectionManager.GetConnection();

            // PublisherConfirmationTrackingEnabled = true causes BasicPublishAsync to await
            // the broker ACK before returning — no manual WaitForConfirmsOrDie needed.
            IChannel channel = await connection.CreateChannelAsync(
                new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                cancellationToken);

            await using (channel)
            {
                BasicProperties props = properties ?? new BasicProperties
                {
                    Persistent = true,
                    MessageId = Guid.NewGuid().ToString(),
                };

                await channel.BasicPublishAsync(
                    exchange: exchange,
                    routingKey: routingKey,
                    mandatory: false,
                    basicProperties: props,
                    body: body,
                    cancellationToken: cancellationToken);

                _logger.LogDebug("Published message to exchange={Exchange} routingKey={RoutingKey}", exchange, routingKey);
            }
        }
    }
}