using Microsoft.EntityFrameworkCore;
using Meezan.DataModel.Entities;
using Meezan.IRepositories.IRepository;
using Meezan.Repositories.Base;
using Meezan.Repositories.Context;

namespace Meezan.Repositories.Repository
{
    internal class MailRepository : BaseRepository<Mail>, IMailRepository
    {
        public MailRepository(Lazy<AppDbContext> appDbContext) : base(appDbContext) { }

        public async Task<Mail?> GetByIdAsync(int mailId, CancellationToken cancellationToken = default)
            => await AppDbContext.Value.Set<Mail>()
                .FirstOrDefaultAsync(m => m.MailId == mailId && !m.IsDeleted, cancellationToken);
    }
}
