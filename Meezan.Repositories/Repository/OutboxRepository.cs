using Microsoft.EntityFrameworkCore;
using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;
using Meezan.IRepositories.IRepository;
using Meezan.Repositories.Base;
using Meezan.Repositories.Context;

namespace Meezan.Repositories.Repository
{
    internal class OutboxRepository : BaseRepository<OutboxMessage>, IOutboxRepository
    {
        public OutboxRepository(Lazy<AppDbContext> appDbContext) : base(appDbContext) { }

        public async Task<List<OutboxMessage>> GetPendingBatchAsync(int batchSize, CancellationToken cancellationToken = default)
            => await AppDbContext.Value.Set<OutboxMessage>()
                .Where(o => o.Status == OutboxStatus.Pending && !o.IsDeleted)
                .OrderBy(o => o.CreationTime)
                .Take(batchSize)
                .Include(o => o.Mail)
                .ToListAsync(cancellationToken);

        public async Task ResetStuckPublishingAsync(CancellationToken cancellationToken = default)
        {
            List<OutboxMessage> stuck = await AppDbContext.Value.Set<OutboxMessage>()
                .Where(o => o.Status == OutboxStatus.Publishing && !o.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (OutboxMessage outbox in stuck)
                outbox.Status = OutboxStatus.Pending;
        }
    }
}