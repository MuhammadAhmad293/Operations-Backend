using Common.CrossCurrencyConversion;
using Common.GoldPurity;
using Common.HijriCalendar;
using MapsterMapper;
using Mapster;
using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;
using Meezan.IRepositories.UnitOfWork;
using Meezan.IServices.IService;
using Meezan.Repositories.Context;
using Meezan.Services.Localization;
using Meezan.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Meezan.Tests.TransactionService
{
    // SQLite in-memory (not EF InMemory): TransactionService.Add/Update/Delete all run inside
    // UnitOfWork.ExecuteInTransactionAsync, which calls Database.BeginTransactionAsync — a
    // relational-only feature the EF InMemory provider (used by the ZakatEngine suite) doesn't
    // support. SQLite gives real transaction semantics while staying fast and fully isolated.
    // The connection must stay open for the in-memory database's lifetime.
    public class TransactionServiceTestFixture : IDisposable
    {
        private static readonly object MapsterLock = new();
        private static bool _mapsterConfigured;

        private readonly SqliteConnection _connection;

        public AppDbContext Context { get; }
        public IUnitOfWork UnitOfWork { get; }
        public IHijriCalendarHelper Hijri { get; } = new HijriCalendarHelper();
        public FakeRateService RateService { get; } = new();
        public IZakatEngine ZakatEngine { get; }
        public ITransactionService TransactionService { get; }
        public IWalletService WalletService { get; }
        public ICategoryService CategoryService { get; }
        public FakeJobEnqueuer JobEnqueuer { get; } = new();

        public TransactionServiceTestFixture()
        {
            lock (MapsterLock)
            {
                if (!_mapsterConfigured)
                {
                    TypeAdapterConfig.GlobalSettings.Scan(typeof(Meezan.Services.TransactionService.TransactionService).Assembly);
                    _mapsterConfigured = true;
                }
            }

            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            // Every EntityConfiguration uses HasDefaultValueSql("GETDATE()") for CreationTime/
            // LastModificationTime — a SQL Server built-in SQLite has no equivalent for. Registering
            // it as a custom function is the standard way to run a SQL-Server-authored EF model
            // against SQLite in tests, with no production code changes.
            _connection.CreateFunction("GETDATE", () => DateTime.UtcNow);

            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;
            Context = new AppDbContext(options);
            Context.Database.EnsureCreated();

            UnitOfWork = new Meezan.Repositories.UnitOfWork.UnitOfWork(new Lazy<AppDbContext>(() => Context));

            Meezan.Services.ZakatEngine.ZakatPotCalculator potCalculator = new(UnitOfWork, RateService);
            ZakatEngine = new Meezan.Services.ZakatEngine.ZakatEngine(UnitOfWork, potCalculator, Hijri);

            IServiceProvider emptyProvider = new ServiceCollection().BuildServiceProvider();
            IMapper mapper = new ServiceMapper(emptyProvider, TypeAdapterConfig.GlobalSettings);
            ILocalizationService localization = new FakeLocalizationService();

            TransactionService = new Meezan.Services.TransactionService.TransactionService(
                UnitOfWork, mapper, localization, RateService, Hijri, ZakatEngine,
                new FakeFileStorageService(), new GoldPurityCalculator(), new CrossCurrencyConversionResolver(),
                NullLogger<Meezan.Services.TransactionService.TransactionService>.Instance);

            WalletService = new Meezan.Services.WalletService.WalletService(UnitOfWork, mapper, localization, RateService, JobEnqueuer, TransactionService, ZakatEngine);
            CategoryService = new Meezan.Services.CategoryService.CategoryService(UnitOfWork, mapper, localization);
        }

        public string Today => Hijri.ToHijriString(DateOnly.FromDateTime(DateTime.UtcNow));

        public async Task<(Account Account, Wallet Wallet, Category ExpenseCategory)> CreateAccountWithWalletAndCategoryAsync(decimal sarBalance)
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

            Category category = new()
            {
                AccountId = account.Id,
                Kind = CategoryKind.Expense,
                Name = "Test Expense",
                SortOrder = 0,
                IsProtected = false,
            };
            UnitOfWork.CategoryRepository.Create(category);
            await UnitOfWork.CommitAsync();

            return (account, wallet, category);
        }

        public async Task<Wallet> AddWalletAsync(Account account, string currencyCode, decimal initialAmount, bool isArchived = false)
        {
            Wallet wallet = new()
            {
                AccountId = account.Id,
                Name = currencyCode,
                WalletTypeId = 3,
                CurrencyCode = currencyCode,
                InitialAmount = initialAmount,
                ExcludeFromTotal = false,
                IsArchived = isArchived,
            };
            UnitOfWork.WalletRepository.Create(wallet);
            await UnitOfWork.CommitAsync();
            return wallet;
        }

        public void Dispose()
        {
            Context.Dispose();
            _connection.Dispose();
        }
    }
}
