using Microsoft.Extensions.Diagnostics.HealthChecks;
using Meezan.DataModel.Enums;
using Meezan.IRepositories.UnitOfWork;

namespace Meezan.Services.HealthChecks
{
    public class OutboxBacklogHealthCheck : IHealthCheck
    {
        private const int DegradedThreshold = 500;
        private const int UnhealthyThreshold = 2000;

        private readonly IUnitOfWork _uow;

        public OutboxBacklogHealthCheck(IUnitOfWork uow) => _uow = uow;

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            int pending = (await _uow.OutboxRepository.GetPendingBatchAsync(UnhealthyThreshold + 1, cancellationToken)).Count;

            if (pending >= UnhealthyThreshold)
                return HealthCheckResult.Unhealthy($"Outbox backlog critical: {pending} pending messages");

            if (pending >= DegradedThreshold)
                return HealthCheckResult.Degraded($"Outbox backlog elevated: {pending} pending messages");

            return HealthCheckResult.Healthy($"Outbox backlog normal: {pending} pending messages");
        }
    }
}