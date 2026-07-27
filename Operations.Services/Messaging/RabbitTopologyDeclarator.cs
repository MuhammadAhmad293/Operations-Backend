using Operations.Services.Setting;
using RabbitMQ.Client;

namespace Operations.Services.Messaging
{
    public static class RabbitTopologyDeclarator
    {
        public static async Task DeclareAsync(IChannel channel, RabbitMqSettings settings, CancellationToken cancellationToken = default)
        {
            // Main exchange
            await channel.ExchangeDeclareAsync(
                exchange: settings.Exchange,
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);

            // Main queue — dead-letters to email.deadletter on NACK
            await channel.QueueDeclareAsync(
                queue: settings.MainQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-dead-letter-exchange"] = "",
                    ["x-dead-letter-routing-key"] = settings.DeadLetterQueue,
                },
                cancellationToken: cancellationToken);

            // Retry queue — TTL expires messages back to main queue
            await channel.QueueDeclareAsync(
                queue: settings.RetryQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-message-ttl"] = settings.RetryTtlMs,
                    ["x-dead-letter-exchange"] = "",
                    ["x-dead-letter-routing-key"] = settings.MainQueue,
                },
                cancellationToken: cancellationToken);

            // Dead-letter queue — terminal failures land here
            await channel.QueueDeclareAsync(
                queue: settings.DeadLetterQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: cancellationToken);

            // Bindings
            await channel.QueueBindAsync(settings.MainQueue, settings.Exchange, settings.MainQueue, cancellationToken: cancellationToken);
            await channel.QueueBindAsync(settings.RetryQueue, settings.Exchange, settings.RetryQueue, cancellationToken: cancellationToken);
            await channel.QueueBindAsync(settings.DeadLetterQueue, settings.Exchange, settings.DeadLetterQueue, cancellationToken: cancellationToken);
        }
    }
}