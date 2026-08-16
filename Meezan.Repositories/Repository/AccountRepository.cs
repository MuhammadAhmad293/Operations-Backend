using Meezan.DataModel.Entities;
using Meezan.IRepositories.IRepository;
using Meezan.Repositories.Base;
using Meezan.Repositories.Context;
using Microsoft.EntityFrameworkCore;

namespace Meezan.Repositories.Repository
{
    public class AccountRepository : BaseRepository<Account>, IAccountRepository
    {
        public AccountRepository(Lazy<AppDbContext> appDbContext) : base(appDbContext)
        {
        }

        public async Task<List<string>> GetDistinctBaseCurrencyCodesAsync()
            => await AppDbContext.Value.Set<Account>()
                .Where(a => !a.IsDeleted)
                .Select(a => a.BaseCurrencyCode)
                .Distinct()
                .ToListAsync();
    }
}
