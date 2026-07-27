using Operations.DataModel.Entities;

namespace Operations.IRepositories.IRepository
{
    public interface IOutboxRepository : IBaseRepository<OutboxMessage>
    {
        Task<List<OutboxMessage>> GetPendingBatchAsync(int batchSize, CancellationToken cancellationToken = default);
        Task ResetStuckPublishingAsync(CancellationToken cancellationToken = default);
    }
}