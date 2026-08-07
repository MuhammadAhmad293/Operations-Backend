using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Meezan.Services.Setting;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Meezan.Services.Messaging
{
    public class RabbitConnectionManager : IHostedService, IDisposable
    {
        private readonly RabbitMqSettings _settings;
        private readonly ILogger<RabbitConnectionManager> _logger;
        private IConnection? _connection;
        private CancellationTokenSource? _retryCts;

        private static readonly TimeSpan[] RetryDelays =
        [
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(30),
        ];

        public RabbitConnectionManager(EmailDeliverySettings settings, ILogger<RabbitConnectionManager> logger)
        {
            _settings = settings.RabbitMq;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _retryCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            // Connect in background — do NOT block host startup
            _ = ConnectWithRetryAsync(_retryCts.Token);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _retryCts?.Cancel();
            if (_connection is not null && _connection.IsOpen)
            {
                await _connection.CloseAsync(cancellationToken);
                _logger.LogInformation("RabbitMQ connection closed");
            }
        }

        public IConnection GetConnection()
        {
            if (_connection is null || !_connection.IsOpen)
                throw new InvalidOperationException("RabbitMQ connection is not available — broker may be starting up");

            return _connection;
        }

        public bool IsConnected => _connection is not null && _connection.IsOpen;

        private async Task ConnectWithRetryAsync(CancellationToken ct)
        {
            ConnectionFactory factory = new()
            {
                HostName = _settings.Host,
                VirtualHost = _settings.VirtualHost,
                UserName = _settings.Username,
                Password = _settings.Password,
                Port = _settings.Port,
            };

            int attempt = 0;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    _connection = await factory.CreateConnectionAsync(ct);
                    _logger.LogInformation("RabbitMQ connection established to {Host}:{Port}", _settings.Host, _settings.Port);
                    return;
                }
                catch (BrokerUnreachableException ex)
                {
                    TimeSpan delay = RetryDelays[Math.Min(attempt, RetryDelays.Length - 1)];
                    _logger.LogWarning(ex,
                        "RabbitMQ unreachable (attempt {Attempt}). Retrying in {Delay}s. Start RabbitMQ or configure EmailDelivery:RabbitMq in appsettings.json",
                        attempt + 1, delay.TotalSeconds);
                    attempt++;
                    await Task.Delay(delay, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    TimeSpan delay = RetryDelays[Math.Min(attempt, RetryDelays.Length - 1)];
                    _logger.LogError(ex, "Unexpected error connecting to RabbitMQ (attempt {Attempt}). Retrying in {Delay}s",
                        attempt + 1, delay.TotalSeconds);
                    attempt++;
                    await Task.Delay(delay, ct);
                }
            }
        }

        public void Dispose()
        {
            _retryCts?.Dispose();
            _connection?.Dispose();
        }
    }
}