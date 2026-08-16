using Common.HijriCalendar;
using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;
using Meezan.IRepositories.UnitOfWork;
using Meezan.IServices.IService;
using Meezan.Repositories.Context;
using Meezan.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Meezan.Tests.ZakatEngine
{
    // A fresh, isolated EF Core InMemory-backed AppDbContext per test — real UnitOfWork/
    // repositories/ZakatEngine/ZakatPotCalculator (production code, unmodified), only IRateService
    // is a test double (rate integration is sub-task 7's concern, not the state machine's).
    // InMemory doesn't enforce decimal(18,3) column rounding the way SQL Server does — this suite
    // is about state-machine *transitions*, not storage-precision rounding (already covered by
    // Phase 012's live verification and this phase's karat-conversion unit tests), so that's an
    // accepted, deliberate scope boundary, not an oversight.
    public class ZakatEngineTestFixture
    {
        public AppDbContext Context { get; }
        public IUnitOfWork UnitOfWork { get; }
        public IHijriCalendarHelper Hijri { get; } = new HijriCalendarHelper();
        public FakeRateService RateService { get; } = new();
        public IZakatEngine Engine { get; }

        public ZakatEngineTestFixture()
        {
            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            Context = new AppDbContext(options);
            Context.Database.EnsureCreated();

            UnitOfWork = new Meezan.Repositories.UnitOfWork.UnitOfWork(new Lazy<AppDbContext>(() => Context));

            Meezan.Services.ZakatEngine.ZakatPotCalculator potCalculator = new(UnitOfWork, RateService);
            Engine = new Meezan.Services.ZakatEngine.ZakatEngine(UnitOfWork, potCalculator, Hijri);
        }

        public string Today => Hijri.ToHijriString(DateOnly.FromDateTime(DateTime.UtcNow));

        // ZakatEngine's methods only ever *stage* their Create/Update calls — every real call
        // site (TransactionService, ZakatService, JobService) persists them by running inside
        // UnitOfWork.ExecuteInTransactionAsync, which SaveChanges-then-commits after the delegate
        // returns (see Phase 012 sub-task 10's JobService fix for the exact same pitfall). Calling
        // ReevaluateAsync/RecomputeCyclePaymentAsync bare, with no flush afterward, is not a
        // scenario production code ever hits — so these tests flush explicitly instead of
        // reaching for ExecuteInTransactionAsync itself, which needs a relational provider
        // (BeginTransactionAsync) that EF Core's InMemory provider doesn't support.
        public async Task ReevaluateAsync(int accountId)
        {
            await Engine.ReevaluateAsync(accountId);
            await UnitOfWork.CommitAsync();
        }

        public async Task RecomputeCyclePaymentAsync(int zakatCycleId)
        {
            await Engine.RecomputeCyclePaymentAsync(zakatCycleId);
            await UnitOfWork.CommitAsync();
        }

        // A SAR cash wallet is enough to drive the pot for these tests — SAR is a seeded, non-
        // metal currency, so its InitialAmount counts toward the pot directly (no karat/purity
        // concept applies), and the FakeRateService supplies a controllable SAR->GOLD rate.
        public async Task<Account> CreateFundedAccountAsync(decimal sarBalance)
        {
            User user = new() { FirstName = "T", LastName = "T", Email = $"{Guid.NewGuid()}@test.local", UserName = Guid.NewGuid().ToString("N"), Password = "x" };
            UnitOfWork.UserRepository.Create(user);
            await UnitOfWork.CommitAsync();

            Account account = new()
            {
                UserId = user.Id,
                Name = "Test Account",
                BaseCurrencyCode = "SAR",
                DisplayCalendar = DisplayCalendar.Hijri,
                Theme = Theme.Dark,
                Language = Language.En,
            };
            UnitOfWork.AccountRepository.Create(account);
            await UnitOfWork.CommitAsync();

            Wallet wallet = new()
            {
                AccountId = account.Id,
                Name = "Cash",
                WalletTypeId = 3,
                CurrencyCode = "SAR",
                InitialAmount = sarBalance,
                ExcludeFromTotal = false,
                IsArchived = false,
            };
            UnitOfWork.WalletRepository.Create(wallet);
            await UnitOfWork.CommitAsync();

            return account;
        }

        public async Task SetWalletBalanceAsync(int accountId, decimal newSarBalance)
        {
            List<Wallet> wallets = await UnitOfWork.WalletRepository.GetByAccountAsync(accountId);
            Wallet wallet = wallets.Single();
            wallet.InitialAmount = newSarBalance;
            UnitOfWork.WalletRepository.Update(wallet);
            await UnitOfWork.CommitAsync();
        }

        public async Task<ZakatCycle?> GetActiveCycleAsync(int accountId)
            => await UnitOfWork.ZakatCycleRepository.GetCurrentActiveByAccountAsync(accountId);

        public async Task<List<ZakatCycle>> GetAllCyclesAsync(int accountId)
            => (await UnitOfWork.ZakatCycleRepository.GetHistoryByAccountAsync(accountId));

        public async Task SetCycleHawlDueHijriAsync(int cycleId, string hawlDueHijri)
        {
            ZakatCycle cycle = await UnitOfWork.ZakatCycleRepository.FirstOrDefaultAsync(z => z.Id == cycleId);
            cycle.HawlDueHijri = hawlDueHijri;
            UnitOfWork.ZakatCycleRepository.Update(cycle);
            await UnitOfWork.CommitAsync();
        }
    }
}
