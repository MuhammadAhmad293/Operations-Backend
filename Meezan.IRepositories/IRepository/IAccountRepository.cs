using Meezan.DataModel.Entities;

namespace Meezan.IRepositories.IRepository
{
    public interface IAccountRepository : IBaseRepository<Account>
    {
        // Every distinct account base currency, system-wide — feeds RateService.SyncAsync's
        // "currencies actually in use" direct-pair fetch (Phase 016), alongside
        // IWalletRepository.GetDistinctActiveCurrencyCodesAsync.
        Task<List<string>> GetDistinctBaseCurrencyCodesAsync();
    }
}
