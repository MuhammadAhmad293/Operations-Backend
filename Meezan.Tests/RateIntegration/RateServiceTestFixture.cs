using System.Net;
using Mapster;
using MapsterMapper;
using Meezan.IServices.IService;
using Meezan.Repositories.Context;
using Meezan.Services.Localization;
using Meezan.Services.RateProviders;
using Meezan.Services.Setting;
using Meezan.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Meezan.Tests.RateIntegration
{
    // SQLite in-memory (same reasoning as TransactionServiceTestFixture — RateService.SyncAsync
    // runs inside UnitOfWork.ExecuteInTransactionAsync). FrankfurterRateProvider is a concrete
    // dependency of RateService (used for currency-sync, not just quotes), so it's wired for real
    // here with its own fake HttpClient returning an empty currency list — currency-sync itself is
    // out of this suite's scope (append-only *rate* persistence is what sub-task 7 asks for).
    public class RateServiceTestFixture : IDisposable
    {
        private static readonly object MapsterLock = new();
        private static bool _mapsterConfigured;

        private readonly SqliteConnection _connection;

        public AppDbContext Context { get; }
        public Meezan.IRepositories.UnitOfWork.IUnitOfWork UnitOfWork { get; }
        public FakeRateProvider RateProvider { get; } = new();
        public FakeHttpMessageHandler FrankfurterHttpHandler { get; } = new();
        public IRateService RateService { get; }

        public RateServiceTestFixture(IDistributedCache? cache = null)
        {
            lock (MapsterLock)
            {
                if (!_mapsterConfigured)
                {
                    TypeAdapterConfig.GlobalSettings.Scan(typeof(Meezan.Services.RateService.RateService).Assembly);
                    _mapsterConfigured = true;
                }
            }

            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            _connection.CreateFunction("GETDATE", () => DateTime.UtcNow);

            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            Context = new AppDbContext(options);
            Context.Database.EnsureCreated();

            UnitOfWork = new Meezan.Repositories.UnitOfWork.UnitOfWork(new Lazy<AppDbContext>(() => Context));

            IServiceProvider emptyProvider = new ServiceCollection().BuildServiceProvider();
            IMapper mapper = new ServiceMapper(emptyProvider, TypeAdapterConfig.GlobalSettings);

            FrankfurterHttpHandler.AlwaysRespondJson(HttpStatusCode.OK, "[]"); // currency-sync / direct-pairs: out of scope by default, override per-test via Enqueue
            RateIntegrationSettings settings = new();
            FrankfurterRateProvider frankfurter = new(new HttpClient(FrankfurterHttpHandler), settings);

            IDistributedCache resolvedCache = cache ?? new FakeDistributedCache();

            RateService = new Meezan.Services.RateService.RateService(
                UnitOfWork, mapper, new FakeLocalizationService(), RateProvider, frankfurter, resolvedCache,
                NullLogger<Meezan.Services.RateService.RateService>.Instance);
        }

        public void Dispose()
        {
            Context.Dispose();
            _connection.Dispose();
        }
    }
}
