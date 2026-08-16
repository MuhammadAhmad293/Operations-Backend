using Microsoft.EntityFrameworkCore;
using Meezan.DataModel.Entities;
using Meezan.IRepositories.IRepository;
using Meezan.Repositories.Base;
using Meezan.Repositories.Context;

namespace Meezan.Repositories.Repository
{
    public class AttachmentRepository : BaseRepository<Attachment>, IAttachmentRepository
    {
        public AttachmentRepository(Lazy<AppDbContext> appDbContext) : base(appDbContext)
        {
        }

        public async Task<List<Attachment>> GetByTransactionIdsAsync(List<int> transactionIds)
            => await AppDbContext.Value.Set<Attachment>()
                .Where(a => transactionIds.Contains(a.TransactionId))
                .ToListAsync();
    }
}
