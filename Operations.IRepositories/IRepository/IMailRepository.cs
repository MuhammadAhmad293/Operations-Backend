using Operations.DataModel.Entities;

namespace Operations.IRepositories.IRepository
{
    public interface IMailRepository : IBaseRepository<Mail>
    {
        Task<Mail?> GetByIdAsync(int mailId, CancellationToken cancellationToken = default);
    }
}
