using Meezan.DataModel.Entities;
using Meezan.IRepositories.IRepository;
using Meezan.Repositories.Base;
using Meezan.Repositories.Context;
using Microsoft.EntityFrameworkCore;

namespace Meezan.Repositories.Repository
{
    public class RateSnapshotRepository : BaseRepository<RateSnapshot>, IRateSnapshotRepository
    {
        public RateSnapshotRepository(Lazy<AppDbContext> appDbContext) : base(appDbContext)
        {
        }

        public async Task<RateSnapshot?> GetLatestByPairAsync(string fromCurrencyCode, string toCurrencyCode, CancellationToken cancellationToken = default)
            => await AppDbContext.Value.Set<RateSnapshot>()
                .Where(r => !r.IsDeleted && r.FromCurrencyCode == fromCurrencyCode && r.ToCurrencyCode == toCurrencyCode)
                .OrderByDescending(r => r.FetchedAt)
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<bool> HasSnapshotSinceAsync(string currencyCode, DateTime cutoff, CancellationToken cancellationToken = default)
            => await AppDbContext.Value.Set<RateSnapshot>()
                .AnyAsync(r => !r.IsDeleted && r.FetchedAt >= cutoff &&
                               (r.FromCurrencyCode == currencyCode /*|| r.ToCurrencyCode == currencyCode*/), cancellationToken);
    }
}
