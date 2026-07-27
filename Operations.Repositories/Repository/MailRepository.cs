using Microsoft.EntityFrameworkCore;
using Operations.DataModel.Entities;
using Operations.IRepositories.IRepository;
using Operations.Repositories.Base;
using Operations.Repositories.Context;

namespace Operations.Repositories.Repository
{
    internal class MailRepository : BaseRepository<Mail>, IMailRepository
    {
        public MailRepository(Lazy<AppDbContext> appDbContext) : base(appDbContext) { }

        public async Task<Mail?> GetByIdAsync(int mailId, CancellationToken cancellationToken = default)
            => await AppDbContext.Value.Set<Mail>()
                .FirstOrDefaultAsync(m => m.MailId == mailId && !m.IsDeleted, cancellationToken);
    }
}
