using Meezan.DataModel.Entities;
using Microsoft.EntityFrameworkCore;

namespace Meezan.Tests.RateIntegration
{
    // meezan-backend.md §7.6 (part 4): RateService.SyncAsync's persistence is append-only — every
    // sync run inserts a brand-new RateSnapshot row per pair, never updates an existing one, so
    // GetLatestByPairAsync's "most recent FetchedAt wins" resolution has real history to pick from.
    public class RateServiceSyncAppendOnlyTests
    {
        private readonly RateServiceTestFixture _fixture = new();

        public RateServiceSyncAppendOnlyTests()
        {
            _fixture.RateProvider
                .AddFiatQuote("USD", "EGP", 49.78m)
                .SetMetalQuote("XAU", "GOLD", 140.0m)
                .SetMetalQuote("XAG", "SILVER", 2.0m);
        }

        [Fact]
        public async Task SyncAsync_InsertsANewRow_RatherThanUpdatingAnExistingOne_OnASecondRun()
        {
            await _fixture.RateService.SyncAsync();
            int countAfterFirstSync = await CountSnapshotsAsync("USD", "EGP");
            Assert.Equal(1, countAfterFirstSync);

            await _fixture.RateService.SyncAsync();
            int countAfterSecondSync = await CountSnapshotsAsync("USD", "EGP");

            Assert.Equal(2, countAfterSecondSync); // appended, not updated in place
        }

        [Fact]
        public async Task SyncAsync_StoresThePerGramRate_ForMetals_NotThePerTroyOunceRate()
        {
            await _fixture.RateService.SyncAsync();

            RateSnapshot goldSnapshot = await SingleSnapshotAsync("GOLD", "USD");
            Assert.Equal(140.0m, goldSnapshot.Rate); // RatePerGram, not the 140*31.1... per-ounce figure

            RateSnapshot silverSnapshot = await SingleSnapshotAsync("SILVER", "USD");
            Assert.Equal(2.0m, silverSnapshot.Rate);
        }

        [Fact]
        public async Task SyncAsync_KeepsEveryHistoricalSnapshot_AcrossManyRuns()
        {
            for (int i = 0; i < 5; i++)
                await _fixture.RateService.SyncAsync();

            Assert.Equal(5, await CountSnapshotsAsync("GOLD", "USD"));
            Assert.Equal(5, await CountSnapshotsAsync("USD", "EGP"));
        }

        private async Task<int> CountSnapshotsAsync(string from, string to)
            => await _fixture.Context.Set<RateSnapshot>().CountAsync(r => r.FromCurrencyCode == from && r.ToCurrencyCode == to);

        private async Task<RateSnapshot> SingleSnapshotAsync(string from, string to)
            => await _fixture.Context.Set<RateSnapshot>().SingleAsync(r => r.FromCurrencyCode == from && r.ToCurrencyCode == to);
    }
}
