using Meezan.DataModel.Entities;
using Meezan.IRepositories.IRepository;
using Meezan.Repositories.Base;
using Meezan.Repositories.Context;

namespace Meezan.Repositories.Repository
{
    public class CurrencyRepository : BaseRepository<Currency>, ICurrencyRepository
    {
        public CurrencyRepository(Lazy<AppDbContext> appDbContext) : base(appDbContext)
        {
        }
    }
}
