using System.Net;
using Meezan.DataModel.Entities;
using Meezan.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Meezan.Tests.RateIntegration
{
    // Phase 016 enhancement: SyncAsync should fetch *direct* pairs for currencies actually in
    // use (an account's base currency, a non-deleted wallet's currency) against each other and
    // against GOLD/SILVER — not just the pre-existing USD-anchored set every fiat currency gets.
    public class RateServiceInUseCurrencySyncTests
    {
        private readonly RateServiceTestFixture _fixture = new();

        public RateServiceInUseCurrencySyncTests()
        {
            _fixture.RateProvider
                .AddFiatQuote("USD", "SAR", 3.75m)
                .AddFiatQuote("USD", "EGP", 49.78m)
                .SetMetalQuote("XAU", "GOLD", 136.81m)
                .SetMetalQuote("XAG", "SILVER", 2.0m);
        }

        [Fact]
        public async Task SyncAsync_FetchesADirectPair_ForAWalletCurrencyAgainstGoldAndSilver()
        {
            // One account, base currency SAR, holding a GOLD wallet — the exact reported bug
            // scenario. SAR is the only in-use *fiat* currency (GOLD is a metal, filtered out as
            // a base — its direct pairs arrive as a *quote* under SAR's call instead), so exactly
            // one batched Frankfurter call is expected: base=SAR, quotes=XAU,XAG.
            SeedAccountAndWallet(baseCurrencyCode: "SAR", walletCurrencyCode: "GOLD");

            _fixture.FrankfurterHttpHandler.EnqueueJson(HttpStatusCode.OK,
                """[{"date":"2026-08-14","base":"SAR","quote":"XAU","rate":0.000061},{"date":"2026-08-14","base":"SAR","quote":"XAG","rate":0.0041}]""");

            await _fixture.RateService.SyncAsync();

            RateSnapshot sarToGold = await SingleSnapshotAsync("SAR", "GOLD");
            Assert.Equal(0.000061m * 31.1034768m, sarToGold.Rate);

            RateSnapshot sarToSilver = await SingleSnapshotAsync("SAR", "SILVER");
            Assert.Equal(0.0041m * 31.1034768m, sarToSilver.Rate);
        }

        [Fact]
        public async Task SyncAsync_FetchesADirectPair_BetweenTwoInUseFiatCurrencies()
        {
            // Two accounts (base SAR and base EGP) — both fiat currencies are "in use" as base
            // currencies, so each becomes its own batched call's base, quoting the other plus
            // GOLD/SILVER. SyncAsync iterates the in-use set in whatever order the DB returns it
            // (not guaranteed to match insertion order), so both responders route on the actual
            // `base=` query value rather than assuming a fixed call order.
            SeedAccountAndWallet(baseCurrencyCode: "SAR", walletCurrencyCode: "SAR");
            SeedAccountAndWallet(baseCurrencyCode: "EGP", walletCurrencyCode: "EGP");

            string sarResponse = """[{"date":"2026-08-14","base":"SAR","quote":"EGP","rate":13.4023},{"date":"2026-08-14","base":"SAR","quote":"XAU","rate":0.000061},{"date":"2026-08-14","base":"SAR","quote":"XAG","rate":0.0041}]""";
            string egpResponse = """[{"date":"2026-08-14","base":"EGP","quote":"SAR","rate":0.07461},{"date":"2026-08-14","base":"EGP","quote":"XAU","rate":0.000005},{"date":"2026-08-14","base":"EGP","quote":"XAG","rate":0.0003}]""";
            HttpResponseMessage RouteByBase(HttpRequestMessage request)
            {
                string json = request.RequestUri!.Query.Contains("base=SAR") ? sarResponse : egpResponse;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
            }
            _fixture.FrankfurterHttpHandler.Enqueue(RouteByBase).Enqueue(RouteByBase);

            await _fixture.RateService.SyncAsync();

            RateSnapshot sarToEgp = await SingleSnapshotAsync("SAR", "EGP");
            Assert.Equal(13.4023m, sarToEgp.Rate); // plain fiat-fiat direct pair, no metal rescale

            RateSnapshot egpToSar = await SingleSnapshotAsync("EGP", "SAR");
            Assert.Equal(0.07461m, egpToSar.Rate);
        }

        [Fact]
        public async Task SyncAsync_SkipsAFailingInUseCurrency_WithoutFailingTheEntireRun()
        {
            // Live-discovered bug (2026-08-14): Frankfurter returns 422 for a currency code it
            // doesn't recognize (e.g. one added to this app's Currency table but not actually
            // supported by the provider). Before the fix, that exception propagated out of
            // FetchDirectPairsForInUseCurrenciesAsync and failed SyncAsync entirely — silently
            // dropping the already-fetched USD-anchored quotes for every other currency too,
            // since the failure happened before SyncAsync's persistence transaction. One bad
            // in-use currency (SAR) must not prevent a good one (EGP) or the pre-existing
            // USD-anchored set from persisting.
            SeedAccountAndWallet(baseCurrencyCode: "SAR", walletCurrencyCode: "SAR");
            SeedAccountAndWallet(baseCurrencyCode: "EGP", walletCurrencyCode: "EGP");

            string egpResponse = """[{"date":"2026-08-14","base":"EGP","quote":"SAR","rate":0.07461},{"date":"2026-08-14","base":"EGP","quote":"XAU","rate":0.000005},{"date":"2026-08-14","base":"EGP","quote":"XAG","rate":0.0003}]""";
            HttpResponseMessage RouteByBase(HttpRequestMessage request)
                => request.RequestUri!.Query.Contains("base=SAR")
                    ? new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(egpResponse, System.Text.Encoding.UTF8, "application/json") };
            _fixture.FrankfurterHttpHandler.Enqueue(RouteByBase).Enqueue(RouteByBase);

            await _fixture.RateService.SyncAsync(); // must not throw

            RateSnapshot egpToSar = await SingleSnapshotAsync("EGP", "SAR"); // the good currency's direct pair still lands
            Assert.Equal(0.07461m, egpToSar.Rate);

            Assert.Equal(0, await _fixture.Context.Set<RateSnapshot>().CountAsync(r => r.FromCurrencyCode == "SAR" && r.ToCurrencyCode == "EGP")); // the failing one just doesn't
            Assert.Equal(1, await _fixture.Context.Set<RateSnapshot>().CountAsync(r => r.FromCurrencyCode == "USD" && r.ToCurrencyCode == "SAR")); // pre-existing USD-anchored set still persisted
            Assert.Equal(1, await _fixture.Context.Set<RateSnapshot>().CountAsync(r => r.FromCurrencyCode == "GOLD" && r.ToCurrencyCode == "USD"));
        }

        [Fact]
        public async Task SyncAsync_FetchesNoDirectPairs_WhenNoAccountsOrWalletsExist()
        {
            // Nothing seeded — FetchDirectPairsForInUseCurrenciesAsync must be a no-op, and in
            // particular must not call Frankfurter's /rates endpoint at all (only the always-on
            // /currencies call from currency-sync should hit the handler).
            await _fixture.RateService.SyncAsync();

            Assert.Equal(1, _fixture.FrankfurterHttpHandler.RequestCount); // just the /currencies call
        }

        private void SeedAccountAndWallet(string baseCurrencyCode, string walletCurrencyCode)
        {
            int userId = _fixture.Context.Set<User>().Count() + 1;
            User user = new()
            {
                FirstName = "Test",
                LastName = "User",
                Email = $"user{userId}@example.com",
                UserName = $"user{userId}",
                Password = "hashed",
            };
            _fixture.Context.Set<User>().Add(user);
            _fixture.Context.SaveChanges();

            Account account = new()
            {
                UserId = user.Id,
                Name = "Test Account",
                BaseCurrencyCode = baseCurrencyCode,
            };
            _fixture.Context.Set<Account>().Add(account);
            _fixture.Context.SaveChanges();

            Wallet wallet = new()
            {
                AccountId = account.Id,
                WalletTypeId = 1, // seeded "General" wallet type
                Name = "Test Wallet",
                CurrencyCode = walletCurrencyCode,
                InitialAmount = 0m,
            };
            _fixture.Context.Set<Wallet>().Add(wallet);
            _fixture.Context.SaveChanges();
        }

        private async Task<RateSnapshot> SingleSnapshotAsync(string from, string to)
            => await _fixture.Context.Set<RateSnapshot>().SingleAsync(r => r.FromCurrencyCode == from && r.ToCurrencyCode == to);
    }
}
