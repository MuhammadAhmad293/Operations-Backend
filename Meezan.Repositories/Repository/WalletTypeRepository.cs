using Meezan.DataModel.Entities;
using Meezan.IRepositories.IRepository;
using Meezan.Repositories.Base;
using Meezan.Repositories.Context;

namespace Meezan.Repositories.Repository
{
    public class WalletTypeRepository : BaseRepository<WalletType>, IWalletTypeRepository
    {
        public WalletTypeRepository(Lazy<AppDbContext> appDbContext) : base(appDbContext)
        {
        }
    }
}
