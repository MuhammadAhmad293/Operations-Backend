using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;

namespace Meezan.Tests.ZakatEngine
{
    // meezan-backend.md §7.2: nisab reach→Active, drop→Broken, full Hijri year→Due, pay→Paid
    // (+ conditional new Active), including Hijri boundary dates. Exercises the real ZakatEngine/
    // ZakatPotCalculator (Phase 012) against an isolated InMemory-backed AppDbContext per test.
    public class ZakatEngineTests
    {
        private const decimal SarToGoldRate = 0.005m; // 1 SAR = 0.005g gold => 85g nisab = 17,000 SAR

        private readonly ZakatEngineTestFixture _fixture = new();

        public ZakatEngineTests()
        {
            _fixture.RateService.SetRate("SAR", "GOLD", SarToGoldRate);
        }

        [Fact]
        public async Task ReevaluateAsync_CreatesActiveCycle_WhenPotReachesNisab()
        {
            Account account = await _fixture.CreateFundedAccountAsync(sarBalance: 20000m); // 100g

            await _fixture.ReevaluateAsync(account.Id);

            ZakatCycle? cycle = await _fixture.GetActiveCycleAsync(account.Id);
            Assert.NotNull(cycle);
            Assert.Equal(ZakatCycleStatus.Active, cycle!.Status);
            Assert.Equal(_fixture.Today, cycle.HawlStartHijri);
            Assert.Equal(_fixture.Hijri.AddHijriYears(_fixture.Today, 1), cycle.HawlDueHijri);
        }

        [Fact]
        public async Task ReevaluateAsync_CountsAGoldWalletsInitialAmount_TowardThePot()
        {
            // Regression test (Phase 017): ZakatPotCalculator used to sum a GOLD wallet's
            // contribution purely from transaction history, silently excluding InitialAmount —
            // unlike every other currency. A wallet with 85g opening gold and zero transactions
            // must reach nisab on its own.
            Account account = await _fixture.CreateGoldFundedAccountAsync(initialGoldGrams: 85m);

            await _fixture.ReevaluateAsync(account.Id);

            ZakatCycle? cycle = await _fixture.GetActiveCycleAsync(account.Id);
            Assert.NotNull(cycle);
            Assert.Equal(ZakatCycleStatus.Active, cycle!.Status);
        }

        [Fact]
        public async Task ReevaluateAsync_CreatesNoCycle_WhenPotStaysBelowNisab()
        {
            Account account = await _fixture.CreateFundedAccountAsync(sarBalance: 1000m); // 5g, well below 85g

            await _fixture.ReevaluateAsync(account.Id);

            Assert.Null(await _fixture.GetActiveCycleAsync(account.Id));
        }

        [Fact]
        public async Task ReevaluateAsync_BreaksActiveCycle_WhenPotDropsBelowNisab()
        {
            Account account = await _fixture.CreateFundedAccountAsync(sarBalance: 20000m);
            await _fixture.ReevaluateAsync(account.Id);
            ZakatCycle active = (await _fixture.GetActiveCycleAsync(account.Id))!;

            await _fixture.SetWalletBalanceAsync(account.Id, 1000m); // drop to 5g, below nisab
            await _fixture.ReevaluateAsync(account.Id);

            Assert.Null(await _fixture.GetActiveCycleAsync(account.Id));
            List<ZakatCycle> all = await _fixture.GetAllCyclesAsync(account.Id);
            ZakatCycle broken = Assert.Single(all, c => c.Id == active.Id);
            Assert.Equal(ZakatCycleStatus.Broken, broken.Status);
        }

        [Fact]
        public async Task ReevaluateAsync_CreatesFreshActiveCycle_AfterBrokenThenReReachingNisab()
        {
            Account account = await _fixture.CreateFundedAccountAsync(sarBalance: 20000m);
            await _fixture.ReevaluateAsync(account.Id);
            ZakatCycle firstCycle = (await _fixture.GetActiveCycleAsync(account.Id))!;

            await _fixture.SetWalletBalanceAsync(account.Id, 1000m);
            await _fixture.ReevaluateAsync(account.Id); // -> Broken

            await _fixture.SetWalletBalanceAsync(account.Id, 20000m);
            await _fixture.ReevaluateAsync(account.Id); // -> new Active

            ZakatCycle? newActive = await _fixture.GetActiveCycleAsync(account.Id);
            Assert.NotNull(newActive);
            Assert.NotEqual(firstCycle.Id, newActive!.Id);

            List<ZakatCycle> all = await _fixture.GetAllCyclesAsync(account.Id);
            Assert.Equal(ZakatCycleStatus.Broken, all.Single(c => c.Id == firstCycle.Id).Status);
        }

        [Fact]
        public async Task ReevaluateAsync_DoesNotTransitionToDue_OneDayBeforeHawlDueHijri()
        {
            Account account = await _fixture.CreateFundedAccountAsync(sarBalance: 20000m);
            await _fixture.ReevaluateAsync(account.Id);
            ZakatCycle active = (await _fixture.GetActiveCycleAsync(account.Id))!;

            string oneDayAway = _fixture.Hijri.AddDays(_fixture.Today, 1);
            await _fixture.SetCycleHawlDueHijriAsync(active.Id, oneDayAway);

            await _fixture.ReevaluateAsync(account.Id);

            ZakatCycle? stillActive = await _fixture.GetActiveCycleAsync(account.Id);
            Assert.NotNull(stillActive);
            Assert.Equal(active.Id, stillActive!.Id);
            Assert.Equal(ZakatCycleStatus.Active, stillActive.Status);
        }

        [Fact]
        public async Task ReevaluateAsync_TransitionsToDue_ExactlyOnHawlDueHijri_AndStartsNewActiveCycle()
        {
            Account account = await _fixture.CreateFundedAccountAsync(sarBalance: 20000m); // 100g pot
            await _fixture.ReevaluateAsync(account.Id);
            ZakatCycle active = (await _fixture.GetActiveCycleAsync(account.Id))!;
            string oldDue = active.HawlDueHijri;

            await _fixture.SetCycleHawlDueHijriAsync(active.Id, _fixture.Today); // exactly due today

            await _fixture.ReevaluateAsync(account.Id);

            List<ZakatCycle> all = await _fixture.GetAllCyclesAsync(account.Id);
            ZakatCycle due = all.Single(c => c.Id == active.Id);
            Assert.Equal(ZakatCycleStatus.Due, due.Status);
            Assert.Equal(100m, due.PotGoldGramsAtDue);
            Assert.Equal(2.5m, due.ZakatDueGoldGrams); // 100g * 2.5%

            ZakatCycle newActive = Assert.Single(all, c => c.Status == ZakatCycleStatus.Active);
            Assert.Equal(_fixture.Today, newActive.HawlStartHijri); // old due date == today here
            Assert.Equal(_fixture.Hijri.AddHijriYears(_fixture.Today, 1), newActive.HawlDueHijri);
        }

        [Fact]
        public async Task ReevaluateAsync_CascadesTwoConsecutiveUnpaidYears_IntoTwoDueCyclesPlusOneActive()
        {
            Account account = await _fixture.CreateFundedAccountAsync(sarBalance: 20000m);
            await _fixture.ReevaluateAsync(account.Id);
            ZakatCycle cycleA = (await _fixture.GetActiveCycleAsync(account.Id))!;

            await _fixture.SetCycleHawlDueHijriAsync(cycleA.Id, _fixture.Today);
            await _fixture.ReevaluateAsync(account.Id); // A -> Due, B created Active

            ZakatCycle cycleB = (await _fixture.GetActiveCycleAsync(account.Id))!;
            Assert.NotEqual(cycleA.Id, cycleB.Id);

            await _fixture.SetCycleHawlDueHijriAsync(cycleB.Id, _fixture.Today);
            await _fixture.ReevaluateAsync(account.Id); // B -> Due, C created Active

            List<ZakatCycle> all = await _fixture.GetAllCyclesAsync(account.Id);
            Assert.Equal(3, all.Count);
            Assert.Equal(ZakatCycleStatus.Due, all.Single(c => c.Id == cycleA.Id).Status);
            Assert.Equal(ZakatCycleStatus.Due, all.Single(c => c.Id == cycleB.Id).Status);
            ZakatCycle cycleC = Assert.Single(all, c => c.Status == ZakatCycleStatus.Active);
            Assert.NotEqual(cycleB.Id, cycleC.Id);
        }

        [Fact]
        public async Task RecomputeCyclePaymentAsync_MarksCyclePaid_WhenLinkedTransactionsCoverTheFullDueAmount()
        {
            Account account = await _fixture.CreateFundedAccountAsync(sarBalance: 20000m);
            ZakatCycle cycle = await SeedDueCycleAsync(account.Id, dueGoldGrams: 5.000m);
            await SeedLinkedZakatTransactionAsync(account.Id, cycle.Id, zakatGoldGrams: 5.000m);

            await _fixture.RecomputeCyclePaymentAsync(cycle.Id);

            ZakatCycle updated = (await _fixture.GetAllCyclesAsync(account.Id)).Single(c => c.Id == cycle.Id);
            Assert.Equal(ZakatCycleStatus.Paid, updated.Status);
        }

        [Fact]
        public async Task RecomputeCyclePaymentAsync_StaysDue_WhenLinkedTransactionsOnlyPartiallyCoverTheDueAmount()
        {
            Account account = await _fixture.CreateFundedAccountAsync(sarBalance: 20000m);
            ZakatCycle cycle = await SeedDueCycleAsync(account.Id, dueGoldGrams: 5.000m);
            await SeedLinkedZakatTransactionAsync(account.Id, cycle.Id, zakatGoldGrams: 3.000m);

            await _fixture.RecomputeCyclePaymentAsync(cycle.Id);

            ZakatCycle updated = (await _fixture.GetAllCyclesAsync(account.Id)).Single(c => c.Id == cycle.Id);
            Assert.Equal(ZakatCycleStatus.Due, updated.Status);
        }

        [Fact]
        public async Task RecomputeCyclePaymentAsync_CountsExternalPayments_TowardTheDueAmount()
        {
            Account account = await _fixture.CreateFundedAccountAsync(sarBalance: 20000m);
            ZakatCycle cycle = await SeedDueCycleAsync(account.Id, dueGoldGrams: 5.000m);
            await SeedLinkedZakatTransactionAsync(account.Id, cycle.Id, zakatGoldGrams: 3.000m);
            cycle.ExternalPaidGoldGrams = 2.000m;
            _fixture.UnitOfWork.ZakatCycleRepository.Update(cycle);
            await _fixture.UnitOfWork.CommitAsync();

            await _fixture.RecomputeCyclePaymentAsync(cycle.Id);

            ZakatCycle updated = (await _fixture.GetAllCyclesAsync(account.Id)).Single(c => c.Id == cycle.Id);
            Assert.Equal(ZakatCycleStatus.Paid, updated.Status);
        }

        private async Task<ZakatCycle> SeedDueCycleAsync(int accountId, decimal dueGoldGrams)
        {
            ZakatCycle cycle = new()
            {
                AccountId = accountId,
                HawlStartHijri = _fixture.Hijri.AddHijriYears(_fixture.Today, -1),
                HawlDueHijri = _fixture.Today,
                Status = ZakatCycleStatus.Due,
                PotGoldGramsAtDue = dueGoldGrams / 0.025m,
                ZakatDueGoldGrams = dueGoldGrams,
            };
            _fixture.UnitOfWork.ZakatCycleRepository.Create(cycle);
            await _fixture.UnitOfWork.CommitAsync();
            return cycle;
        }

        private async Task SeedLinkedZakatTransactionAsync(int accountId, int cycleId, decimal zakatGoldGrams)
        {
            List<Meezan.DataModel.Entities.Wallet> wallets = await _fixture.UnitOfWork.WalletRepository.GetByAccountAsync(accountId);
            Transaction transaction = new()
            {
                AccountId = accountId,
                Type = TransactionType.Expense,
                DateGregorian = DateOnly.FromDateTime(DateTime.UtcNow),
                DateHijri = _fixture.Today,
                Time = TimeOnly.FromDateTime(DateTime.UtcNow),
                Amount = 100m,
                WalletId = wallets.Single().Id,
                ZakatCycleId = cycleId,
                ZakatGoldGrams = zakatGoldGrams,
            };
            _fixture.UnitOfWork.TransactionRepository.Create(transaction);
            await _fixture.UnitOfWork.CommitAsync();
        }
    }
}
