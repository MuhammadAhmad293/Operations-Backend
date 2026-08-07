using RabbitMQ.Client;

namespace Meezan.Services.Messaging
{
    public interface IRabbitPublisher
    {
        bool IsReady { get; }
        Task PublishAsync(string exchange, string routingKey, byte[] body, BasicProperties? properties = null, CancellationToken cancellationToken = default);
    }
}