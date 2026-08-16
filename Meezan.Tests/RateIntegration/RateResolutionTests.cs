using Meezan.DataModel.Entities;
using Meezan.Services.CustomExceptions;
using Meezan.Tests.TestSupport;

namespace Meezan.Tests.RateIntegration
{
    // GetLatestAsync's direct/inverse/cross-through-USD resolution (spec §7 / plan Phase 010
    // sub-task 7), exercised against the DB path only — every test seeds RateSnapshot rows
    // directly (bypassing SyncAsync/RateProvider) with only USD-anchored pairs, matching what a
    // real sync populates: USD->SAR, USD->EGP, GOLD->USD, SILVER->USD. Neither SAR->GOLD nor
    // EGP->SAR nor any inverse direction is ever stored directly, so a resolved result proves the
    // derivation actually ran.
    //
    // Each case runs twice: once against a reachable-but-empty FakeDistributedCache (a cache miss
    // that falls through to the DB) and once against a ThrowingDistributedCache that fails every
    // call the way a genuinely-down Redis does (BR-19 "never blocks"). Identical results across
    // both prove the cross-through-USD path is reached from the DB-fallback branch, not just the
    // cache-hit branch.
    public class RateResolutionTests
    {
        private const decimal UsdToSar = 3.75m;
        private const decimal UsdToEgp = 49.78m;
        private const decimal GoldToUsdPerGram = 136.81m;
        private const decimal SilverToUsdPerGram = 2.0m;

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GetLatestAsync_ResolvesDirect_FromTheStoredPair(bool redisDown)
        {
            using RateServiceTestFixture fixture = CreateFixture(redisDown);

            decimal rate = await fixture.RateService.GetLatestAsync("USD", "SAR");

            Assert.Equal(UsdToSar, rate);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GetLatestAsync_ResolvesInverse_WhenOnlyTheReverseDirectionIsStored(bool redisDown)
        {
            using RateServiceTestFixture fixture = CreateFixture(redisDown);

            decimal rate = await fixture.RateService.GetLatestAsync("SAR", "USD");

            Assert.Equal(1m / UsdToSar, rate);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GetLatestAsync_ResolvesCrossThroughUsd_ForAFiatToFiatPair(bool redisDown)
        {
            using RateServiceTestFixture fixture = CreateFixture(redisDown);

            // EGP->SAR = (1/USD->EGP) * USD->SAR — neither leg nor the pair itself is stored directly.
            decimal rate = await fixture.RateService.GetLatestAsync("EGP", "SAR");

            decimal expected = (1m / UsdToEgp) * UsdToSar;
            Assert.Equal(expected, rate);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GetLatestAsync_ResolvesCrossThroughUsd_ForAFiatToMetalPair(bool redisDown)
        {
            using RateServiceTestFixture fixture = CreateFixture(redisDown);

            // SAR->GOLD = (1/USD->SAR) * (1/GOLD->USD) — this is exactly the reported bug scenario:
            // base currency SAR, a GOLD wallet, only USD-anchored snapshots in the table.
            decimal rate = await fixture.RateService.GetLatestAsync("SAR", "GOLD");

            decimal expected = (1m / UsdToSar) * (1m / GoldToUsdPerGram);
            Assert.Equal(expected, rate);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GetLatestAsync_Throws_WhenNoCombinationOfSnapshotsResolvesThePair(bool redisDown)
        {
            using RateServiceTestFixture fixture = CreateFixture(redisDown);

            await Assert.ThrowsAsync<RatesUnavailableException>(
                () => fixture.RateService.GetLatestAsync("SAR", "ZZZ"));
        }

        private static RateServiceTestFixture CreateFixture(bool redisDown)
        {
            RateServiceTestFixture fixture = new(redisDown ? new ThrowingDistributedCache() : new FakeDistributedCache());
            SeedUsdAnchoredSnapshots(fixture);
            return fixture;
        }

        private static void SeedUsdAnchoredSnapshots(RateServiceTestFixture fixture)
        {
            DateTime fetchedAt = DateTime.UtcNow;
            fixture.Context.Set<RateSnapshot>().AddRange(
                new RateSnapshot { FromCurrencyCode = "USD", ToCurrencyCode = "SAR", Rate = UsdToSar, FetchedAt = fetchedAt, Source = "Fake" },
                new RateSnapshot { FromCurrencyCode = "USD", ToCurrencyCode = "EGP", Rate = UsdToEgp, FetchedAt = fetchedAt, Source = "Fake" },
                new RateSnapshot { FromCurrencyCode = "GOLD", ToCurrencyCode = "USD", Rate = GoldToUsdPerGram, FetchedAt = fetchedAt, Source = "Fake" },
                new RateSnapshot { FromCurrencyCode = "SILVER", ToCurrencyCode = "USD", Rate = SilverToUsdPerGram, FetchedAt = fetchedAt, Source = "Fake" });
            fixture.Context.SaveChanges();
        }
    }
}
