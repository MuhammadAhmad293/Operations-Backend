using Meezan.DataModel.Entities;

namespace Meezan.IRepositories.IRepository
{
    public interface IMailRepository : IBaseRepository<Mail>
    {
        Task<Mail?> GetByIdAsync(int mailId, CancellationToken cancellationToken = default);
    }
}
