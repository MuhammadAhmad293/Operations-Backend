using Meezan.DataModel.Entities;

namespace Meezan.IRepositories.IRepository
{
    public interface IRateSnapshotRepository : IBaseRepository<RateSnapshot>
    {
        // Most recent append-only snapshot for one exact (from, to) direction — the
        // (FromCurrencyCode, ToCurrencyCode, FetchedAt) index makes this a single index seek.
        Task<RateSnapshot?> GetLatestByPairAsync(string fromCurrencyCode, string toCurrencyCode, CancellationToken cancellationToken = default);

        // Whether *any* snapshot involving this currency (either side of the pair) was fetched
        // on or after cutoff — feeds the "does a brand-new wallet currency already have usable
        // data" check (Phase 016 sub-task 3), not a specific pair.
        Task<bool> HasSnapshotSinceAsync(string currencyCode, DateTime cutoff, CancellationToken cancellationToken = default);
    }
}
