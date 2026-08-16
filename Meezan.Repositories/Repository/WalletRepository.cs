using Microsoft.EntityFrameworkCore;
using Meezan.DataModel.Entities;
using Meezan.IRepositories.IRepository;
using Meezan.Repositories.Base;
using Meezan.Repositories.Context;

namespace Meezan.Repositories.Repository
{
    public class WalletRepository : BaseRepository<Wallet>, IWalletRepository
    {
        public WalletRepository(Lazy<AppDbContext> appDbContext) : base(appDbContext)
        {
        }

        public async Task<List<Wallet>> GetByAccountAsync(int accountId)
            => await AppDbContext.Value.Set<Wallet>()
                .Where(w => w.AccountId == accountId && !w.IsDeleted)
                .ToListAsync();
        public async Task<List<Wallet>> GetIncludedWalletsByAccountAsync(int accountId)
            => await AppDbContext.Value.Set<Wallet>()
                .Where(w => w.AccountId == accountId && !w.ExcludeFromTotal && !w.IsDeleted)
                .ToListAsync();

        public async Task<List<Wallet>> GetByAccountIncludingDeletedAsync(int accountId)
            => await AppDbContext.Value.Set<Wallet>()
                .Where(w => w.AccountId == accountId)
                .ToListAsync();

        public async Task<List<string>> GetDistinctActiveCurrencyCodesAsync()
            => await AppDbContext.Value.Set<Wallet>()
                .Where(w => !w.IsDeleted)
                .Select(w => w.CurrencyCode)
                .Distinct()
                .ToListAsync();
    }
}
