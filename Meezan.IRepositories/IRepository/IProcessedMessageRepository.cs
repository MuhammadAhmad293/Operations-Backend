using Meezan.DataModel.Entities;

namespace Meezan.IRepositories.IRepository
{
    public interface IProcessedMessageRepository : IBaseRepository<ProcessedMessage>
    {
        Task<bool> ExistsAsync(string messageId, CancellationToken cancellationToken = default);
    }
}