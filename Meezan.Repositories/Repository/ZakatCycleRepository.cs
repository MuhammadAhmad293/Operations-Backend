using Microsoft.EntityFrameworkCore;
using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;
using Meezan.IRepositories.IRepository;
using Meezan.Repositories.Base;
using Meezan.Repositories.Context;

namespace Meezan.Repositories.Repository
{
    public class ZakatCycleRepository : BaseRepository<ZakatCycle>, IZakatCycleRepository
    {
        public ZakatCycleRepository(Lazy<AppDbContext> appDbContext) : base(appDbContext)
        {
        }

        public async Task<ZakatCycle?> GetCurrentActiveByAccountAsync(int accountId)
            => await AppDbContext.Value.Set<ZakatCycle>()
                .FirstOrDefaultAsync(z => z.AccountId == accountId && z.Status == ZakatCycleStatus.Active);

        public async Task<List<ZakatCycle>> GetDueByAccountAsync(int accountId)
            => await AppDbContext.Value.Set<ZakatCycle>()
                .Where(z => z.AccountId == accountId && z.Status == ZakatCycleStatus.Due)
                .OrderBy(z => z.HawlDueHijri)
                .ToListAsync();

        public async Task<List<ZakatCycle>> GetHistoryByAccountAsync(int accountId)
            => await AppDbContext.Value.Set<ZakatCycle>()
                .Where(z => z.AccountId == accountId)
                .OrderByDescending(z => z.HawlStartHijri)
                .ToListAsync();
    }
}
