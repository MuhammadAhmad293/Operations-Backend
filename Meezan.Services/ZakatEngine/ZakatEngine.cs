using Common.HijriCalendar;
using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;
using Meezan.IRepositories.UnitOfWork;
using Meezan.IServices.IService;

namespace Meezan.Services.ZakatEngine
{
    // UC-22 hawl state machine (BR-13/14/15, as revised by the Live Spec Correction). Pot/time-
    // driven only — never touches a cycle's payment fields (PaidGoldGrams/ExternalPaidGoldGrams/
    // the Paid status), that's RecomputeCyclePaymentAsync's separate concern. Called from every
    // balance-affecting Transaction write (Phase 009's create/edit/delete hooks, already wired)
    // and from the daily rate-sync job (wired in sub-task 10).
    public class ZakatEngine : IZakatEngine
    {
        private IUnitOfWork UnitOfWork { get; }
        private IZakatPotCalculator PotCalculator { get; }
        private IHijriCalendarHelper HijriCalendarHelper { get; }

        public ZakatEngine(IUnitOfWork unitOfWork, IZakatPotCalculator potCalculator, IHijriCalendarHelper hijriCalendarHelper)
        {
            UnitOfWork = unitOfWork;
            PotCalculator = potCalculator;
            HijriCalendarHelper = hijriCalendarHelper;
        }

        public async Task ReevaluateAsync(int accountId, CancellationToken cancellationToken = default)
        {
            // Callers (TransactionService.Add/Update/Delete) invoke this from inside their own
            // ExecuteInTransactionAsync delegate, immediately after staging the triggering
            // Transaction change — EF Core does not auto-flush pending changes before a fresh
            // query, so without this the pot computation below would read the wallet's balance
            // as it was *before* that change. Flushing here (still inside the caller's ambient
            // DB transaction — this only calls SaveChangesAsync, not a commit) makes it visible
            // to this method's own queries without affecting the outer transaction's atomicity.
            await UnitOfWork.CommitAsync(cancellationToken);

            decimal pot = await PotCalculator.ComputePotGoldGramsAsync(accountId, cancellationToken);
            string today = HijriCalendarHelper.ToHijriString(DateOnly.FromDateTime(DateTime.UtcNow));

            ZakatCycle? activeCycle = await UnitOfWork.ZakatCycleRepository.GetCurrentActiveByAccountAsync(accountId);

            if (activeCycle is null)
            {
                // BR-14: nisab reached with no cycle running — start one, dated today (the day
                // the crossing was detected; re-evaluation is triggered by every balance-affecting
                // transaction, so this is same-day for any user-caused crossing).
                if (pot >= ZakatPotCalculator.NisabGoldGrams)
                {
                    UnitOfWork.ZakatCycleRepository.Create(new ZakatCycle
                    {
                        AccountId = accountId,
                        HawlStartHijri = today,
                        HawlDueHijri = HijriCalendarHelper.AddHijriYears(today, 1),
                        Status = ZakatCycleStatus.Active,
                    });
                }
                return;
            }

            if (pot < ZakatPotCalculator.NisabGoldGrams)
            {
                // BR-15: dropped below nisab before the year completed — Broken, only ever the
                // current Active cycle (any already-Due cycles are untouched, a separate concern).
                activeCycle.Status = ZakatCycleStatus.Broken;
                UnitOfWork.ZakatCycleRepository.Update(activeCycle);
                return;
            }

            // pot >= nisab: cascade through every hawl-due boundary crossed since the last
            // evaluation (normally at most one — daily re-evaluation means a multi-boundary gap
            // is an edge case, but the loop makes this self-healing for however long the gap is).
            // Each cascaded Due transition is valued at *this* call's single pot figure — there's
            // no historical per-day pot record to value an older boundary more precisely against;
            // this is an inherent limit of trigger-based (not continuous) re-evaluation.
            while (string.CompareOrdinal(today, activeCycle.HawlDueHijri) >= 0)
            {
                // Live Spec Correction: Due is unconditional and time-driven — never gated on the
                // cycle's own payment status, and never touches PaidGoldGrams/ExternalPaidGoldGrams.
                activeCycle.PotGoldGramsAtDue = pot;
                activeCycle.ZakatDueGoldGrams = pot * ZakatPotCalculator.ZakatRate;
                activeCycle.Status = ZakatCycleStatus.Due;
                UnitOfWork.ZakatCycleRepository.Update(activeCycle);

                // Live Spec Correction: unconditionally start a new Active cycle the same Hijri
                // day the old one became due — never gated on whether the old one gets paid.
                ZakatCycle newActiveCycle = new()
                {
                    AccountId = accountId,
                    HawlStartHijri = activeCycle.HawlDueHijri,
                    HawlDueHijri = HijriCalendarHelper.AddHijriYears(activeCycle.HawlDueHijri, 1),
                    Status = ZakatCycleStatus.Active,
                };
                UnitOfWork.ZakatCycleRepository.Create(newActiveCycle);
                activeCycle = newActiveCycle;
            }
        }

        // Round-2 refinement: the payment state transition, entirely separate from the hawl
        // transition above — never creates/mutates any cycle other than the one passed in, and
        // never touches HawlStartHijri/HawlDueHijri/PotGoldGramsAtDue/ZakatDueGoldGrams.
        public async Task RecomputeCyclePaymentAsync(int zakatCycleId, CancellationToken cancellationToken = default)
        {
            // Same reasoning as ReevaluateAsync: this runs inside the caller's own
            // ExecuteInTransactionAsync delegate, right after the triggering Zakat-linked
            // Transaction was created/updated/deleted — flush first so the sum below sees it.
            await UnitOfWork.CommitAsync(cancellationToken);

            ZakatCycle? cycle = await UnitOfWork.ZakatCycleRepository.FirstOrDefaultAsync(z => z.Id == zakatCycleId);
            if (cycle is null)
                return;

            decimal paidViaExpenses = await UnitOfWork.TransactionRepository.GetZakatGoldGramsSumByCycleAsync(zakatCycleId);
            decimal paidGoldGrams = paidViaExpenses + (cycle.ExternalPaidGoldGrams ?? 0m);

            // Paid once fully covered; reverts to Due if a deletion drops it back below (the
            // only two statuses this method ever assigns — it's only ever called for a cycle
            // that's already Due or Paid, never Active/Broken).
            cycle.Status = paidGoldGrams >= (cycle.ZakatDueGoldGrams ?? 0m) ? ZakatCycleStatus.Paid : ZakatCycleStatus.Due;
            UnitOfWork.ZakatCycleRepository.Update(cycle);
        }
    }
}
