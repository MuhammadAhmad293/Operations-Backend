using Meezan.DataModel.Entities;

namespace Meezan.IRepositories.IRepository
{
    public interface IOutboxRepository : IBaseRepository<OutboxMessage>
    {
        Task<List<OutboxMessage>> GetPendingBatchAsync(int batchSize, CancellationToken cancellationToken = default);
        Task ResetStuckPublishingAsync(CancellationToken cancellationToken = default);
    }
}