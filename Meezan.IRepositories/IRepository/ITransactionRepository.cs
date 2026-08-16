using Meezan.DataModel.Entities;

namespace Meezan.IRepositories.IRepository
{
    public interface ITransactionRepository : IBaseRepository<Transaction>
    {
        Task<bool> ExistsForWalletAsync(int walletId);
        Task<bool> ExistsForCategoryAsync(int categoryId);
        // asOfDateInclusive bounds the sum to transactions dated on or before that date — used
        // by Statistics (Phase 011) to compute a wallet's balance at a past point in time
        // (opening/ending balance). Omitted (null) preserves the original unbounded/current-
        // balance behavior every existing caller (Wallet/Account) relies on.
        Task<decimal> GetSignedSumForWalletAsync(int walletId, DateOnly? asOfDateInclusive = null);

        // Same FROM/TO signed-sum shape as GetSignedSumForWalletAsync, but the FROM side sums
        // PureGoldGrams instead of raw Amount — BR-03: Zakat always uses the pure 24K equivalent,
        // never the raw purchased grams a wallet screen shows. Only meaningful for GOLD-currency
        // wallets (Phase 012's pot computation); PureGoldGrams is always set on a GOLD wallet's
        // own transactions, so this is never actually a mixed-currency query.
        Task<decimal> GetSignedPureGoldGramsSumForWalletAsync(int walletId);

        // Round-2 refinement: PaidGoldGrams = Σ stored ZakatGoldGrams of a cycle's linked
        // expenses — sums only what's already on each Transaction row, never re-fetches a rate.
        Task<decimal> GetZakatGoldGramsSumByCycleAsync(int zakatCycleId);
        Task<List<Transaction>> GetFilteredAsync(int accountId, TransactionFilter filter);
        Task<List<Transaction>> SearchAsync(int accountId, string query);
        Task<List<Transaction>> GetFeesByParentIdAsync(int parentTransactionId);
    }
}
