using Meezan.DataModel.Entities;

namespace Meezan.IRepositories.IRepository
{
    public interface IZakatCycleRepository : IBaseRepository<ZakatCycle>
    {
        // At most one per account by construction (ReevaluateAsync never creates a second Active
        // cycle while one already exists) — Active is the currently-running hawl, if any.
        Task<ZakatCycle?> GetCurrentActiveByAccountAsync(int accountId);

        // Every unpaid debt (Status = Due) — the Live Spec Correction allows multiple simultaneous
        // Due cycles (consecutive unpaid years), all of which are valid targets for PayExternal's
        // allow-list and both appear in GetStatus's total-outstanding-debt sum.
        Task<List<ZakatCycle>> GetDueByAccountAsync(int accountId);

        // Every cycle regardless of status — the Zakat page's full history list (GetCycles).
        Task<List<ZakatCycle>> GetHistoryByAccountAsync(int accountId);
    }
}
