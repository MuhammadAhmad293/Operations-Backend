using Microsoft.EntityFrameworkCore;
using Operations.DataModel.Entities;
using Operations.IRepositories.IRepository;
using Operations.Repositories.Base;
using Operations.Repositories.Context;

namespace Operations.Repositories.Repository
{
    internal class ProcessedMessageRepository : BaseRepository<ProcessedMessage>, IProcessedMessageRepository
    {
        public ProcessedMessageRepository(Lazy<AppDbContext> appDbContext) : base(appDbContext) { }

        public async Task<bool> ExistsAsync(string messageId, CancellationToken cancellationToken = default)
            => await AppDbContext.Value.Set<ProcessedMessage>()
                .AnyAsync(p => p.MessageId == messageId && !p.IsDeleted, cancellationToken);
    }
}