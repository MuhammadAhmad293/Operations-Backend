using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Meezan.Services.HealthChecks
{
    public class RedisHealthCheck : IHealthCheck
    {
        private readonly IConnectionMultiplexer _connectionMultiplexer;

        public RedisHealthCheck(IConnectionMultiplexer connectionMultiplexer)
            => _connectionMultiplexer = connectionMultiplexer;

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                IDatabase database = _connectionMultiplexer.GetDatabase();
                await database.PingAsync();
                return HealthCheckResult.Healthy("Redis connection is open");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Redis connection is unavailable", ex);
            }
        }
    }
}
