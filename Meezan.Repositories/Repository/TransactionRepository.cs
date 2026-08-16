using Microsoft.EntityFrameworkCore;
using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;
using Meezan.IRepositories.IRepository;
using Meezan.Repositories.Base;
using Meezan.Repositories.Context;

namespace Meezan.Repositories.Repository
{
    public class TransactionRepository : BaseRepository<Transaction>, ITransactionRepository
    {
        public TransactionRepository(Lazy<AppDbContext> appDbContext) : base(appDbContext)
        {
        }

        public async Task<bool> ExistsForWalletAsync(int walletId)
            => await AppDbContext.Value.Set<Transaction>()
                .AnyAsync(t => t.WalletId == walletId || t.ToWalletId == walletId);

        public async Task<bool> ExistsForCategoryAsync(int categoryId)
            => await AppDbContext.Value.Set<Transaction>()
                .AnyAsync(t => t.CategoryId == categoryId);

        // Net signed effect of every transaction on a wallet's balance. Income adds; Expense and
        // Transfer-as-source both subtract by Amount (Fee transactions are Type=Expense, so they
        // fall out of this naturally — no separate IsFee handling needed). Transfer-as-destination
        // credits ConvertedAmount when a cross-currency rate applied, else the same-currency Amount.
        public async Task<decimal> GetSignedSumForWalletAsync(int walletId, DateOnly? asOfDateInclusive = null)
        {
            IQueryable<Transaction> dbSet = AppDbContext.Value.Set<Transaction>();
            if (asOfDateInclusive.HasValue)
                dbSet = dbSet.Where(t => t.DateGregorian <= asOfDateInclusive.Value);

            decimal fromWallet = await dbSet
                .Where(t => t.WalletId == walletId && !t.IsDeleted)
                .SumAsync(t => t.Type == TransactionType.Income ? t.Amount : -t.Amount);

            decimal toWallet = await dbSet
                .Where(t => t.ToWalletId == walletId && !t.IsDeleted)
                .SumAsync(t => t.ConvertedAmount ?? t.Amount);

            return fromWallet + toWallet;
        }

        // Same shape as GetSignedSumForWalletAsync, but the FROM side sums PureGoldGrams — always
        // set for a GOLD wallet's own transactions (ComputeKarat never returns null for GOLD),
        // the `?? t.Amount` is a defensive fallback only. The TO side is unchanged: a transfer-in
        // credit's ConvertedAmount is already pure-gold-equivalent by construction (computed via
        // the pure-24K GOLD market rate), so no separate pure-grams field exists for it.
        public async Task<decimal> GetSignedPureGoldGramsSumForWalletAsync(int walletId)
        {
            IQueryable<Transaction> dbSet = AppDbContext.Value.Set<Transaction>();

            decimal fromWallet = await dbSet
                .Where(t => t.WalletId == walletId && !t.IsDeleted)
                .SumAsync(t => t.Type == TransactionType.Income ? (t.PureGoldGrams ?? t.Amount) : -(t.PureGoldGrams ?? t.Amount));

            decimal toWallet = await dbSet
                .Where(t => t.ToWalletId == walletId && !t.IsDeleted)
                .SumAsync(t => t.ConvertedAmount ?? t.Amount);

            return fromWallet + toWallet;
        }

        public async Task<decimal> GetZakatGoldGramsSumByCycleAsync(int zakatCycleId)
            => await AppDbContext.Value.Set<Transaction>()
                .Where(t => t.ZakatCycleId == zakatCycleId && !t.IsDeleted)
                .SumAsync(t => t.ZakatGoldGrams ?? 0m);

        public async Task<List<Transaction>> GetFilteredAsync(int accountId, TransactionFilter filter)
        {
            IQueryable<Transaction> query = AppDbContext.Value.Set<Transaction>()
                .Where(t => t.AccountId == accountId && !t.IsDeleted);

            if (filter.From.HasValue)
                query = query.Where(t => t.DateGregorian >= filter.From.Value);
            if (filter.To.HasValue)
                query = query.Where(t => t.DateGregorian <= filter.To.Value);
            if (filter.WalletId.HasValue)
                query = query.Where(t => t.WalletId == filter.WalletId.Value || t.ToWalletId == filter.WalletId.Value);
            if (filter.CategoryId.HasValue)
                query = query.Where(t => t.CategoryId == filter.CategoryId.Value);
            if (filter.Type.HasValue)
                query = query.Where(t => t.Type == filter.Type.Value);

            return await query
                .OrderByDescending(t => t.DateGregorian)
                .ThenByDescending(t => t.Time)
                .ToListAsync();
        }

        public async Task<List<Transaction>> SearchAsync(int accountId, string query)
        {
            string pattern = $"%{query}%";
            return await AppDbContext.Value.Set<Transaction>()
                .Where(t => t.AccountId == accountId && !t.IsDeleted &&
                    (EF.Functions.Like(t.Description, pattern) ||
                     EF.Functions.Like(t.Note, pattern) ||
                     (t.Category != null && EF.Functions.Like(t.Category.Name, pattern)) ||
                     EF.Functions.Like(t.Wallet.Name, pattern)))
                .OrderByDescending(t => t.DateGregorian)
                .ThenByDescending(t => t.Time)
                .ToListAsync();
        }

        // BR-09: the fee self-reference FK is Restrict at the DB level (SQL Server rejects the
        // cascade — see Phase 005 sub-task 12's notes), so the caller must find and delete any
        // linked fee explicitly rather than relying on a DB cascade.
        public async Task<List<Transaction>> GetFeesByParentIdAsync(int parentTransactionId)
            => await AppDbContext.Value.Set<Transaction>()
                .Where(t => t.ParentTransactionId == parentTransactionId && !t.IsDeleted)
                .ToListAsync();
    }
}
