using Operations.DataModel.Entities;

namespace Operations.IRepositories.IRepository
{
    public interface IProcessedMessageRepository : IBaseRepository<ProcessedMessage>
    {
        Task<bool> ExistsAsync(string messageId, CancellationToken cancellationToken = default);
    }
}