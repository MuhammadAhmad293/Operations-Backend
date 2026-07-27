using Microsoft.EntityFrameworkCore;
using Operations.DataModel.Entities;
using Operations.DataModel.Enums;
using Operations.IRepositories.IRepository;
using Operations.Repositories.Base;
using Operations.Repositories.Context;

namespace Operations.Repositories.Repository
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