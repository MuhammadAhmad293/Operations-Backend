using Meezan.DataModel.Entities;

namespace Meezan.IRepositories.IRepository
{
    public interface IAttachmentRepository : IBaseRepository<Attachment>
    {
        Task<List<Attachment>> GetByTransactionIdsAsync(List<int> transactionIds);
    }
}
