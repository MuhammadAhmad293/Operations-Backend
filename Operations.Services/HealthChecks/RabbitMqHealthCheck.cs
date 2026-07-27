using Microsoft.Extensions.Diagnostics.HealthChecks;
using Operations.Services.Messaging;

namespace Operations.Services.HealthChecks
{
    public class RabbitMqHealthCheck : IHealthCheck
    {
        private readonly RabbitConnectionManager _connectionManager;

        public RabbitMqHealthCheck(RabbitConnectionManager connectionManager)
            => _connectionManager = connectionManager;

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                _connectionManager.GetConnection();
                return Task.FromResult(HealthCheckResult.Healthy("RabbitMQ connection is open"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ connection is unavailable", ex));
            }
        }
    }
}