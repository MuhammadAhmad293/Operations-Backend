using Meezan.DataModel.Entities;

namespace Meezan.IRepositories.IRepository
{
    public interface IWalletRepository : IBaseRepository<Wallet>
    {
        Task<List<Wallet>> GetByAccountAsync(int accountId);
        Task<List<Wallet>> GetIncludedWalletsByAccountAsync(int accountId);

        // Includes soft-deleted wallets — BR-06: a wallet's historical transactions must keep
        // displaying/counting even after the wallet itself is soft-deleted, so read-side
        // aggregates (Overview/Calendar/Statistics) need every wallet a past transaction could
        // reference, not just the currently-selectable ones GetByAccountAsync returns.
        Task<List<Wallet>> GetByAccountIncludingDeletedAsync(int accountId);

        // Every currency code held by a non-deleted wallet, system-wide, de-duplicated at the DB
        // level — feeds RateService.SyncAsync's "currencies actually in use" direct-pair fetch
        // (Phase 016). Soft-deleted wallets are excluded: their currency no longer needs fresh
        // rates just because a past transaction still references it.
        Task<List<string>> GetDistinctActiveCurrencyCodesAsync();
    }
}
