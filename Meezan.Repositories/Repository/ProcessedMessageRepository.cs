using Microsoft.EntityFrameworkCore;
using Meezan.DataModel.Entities;
using Meezan.IRepositories.IRepository;
using Meezan.Repositories.Base;
using Meezan.Repositories.Context;

namespace Meezan.Repositories.Repository
{
    internal class ProcessedMessageRepository : BaseRepository<ProcessedMessage>, IProcessedMessageRepository
    {
        public ProcessedMessageRepository(Lazy<AppDbContext> appDbContext) : base(appDbContext) { }

        public async Task<bool> ExistsAsync(string messageId, CancellationToken cancellationToken = default)
            => await AppDbContext.Value.Set<ProcessedMessage>()
                .AnyAsync(p => p.MessageId == messageId && !p.IsDeleted, cancellationToken);
    }
}