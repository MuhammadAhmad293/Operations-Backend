using Meezan.DataModel.Entities;
using Meezan.Dto.DTOs.Wallet;
using Meezan.Tests.TransactionService;

namespace Meezan.Tests.WalletService
{
    // Phase 016 sub-task 3: adding a wallet in a currency with no recent rate data must enqueue
    // a background sync rather than ever calling a rate provider inline (BR-19). WalletService
    // never touches Hangfire directly (Meezan.Services has no Hangfire dependency — see
    // IJobEnqueuer) — it only decides *whether* a sync is needed and delegates to the adapter,
    // which is exactly what these tests assert via FakeJobEnqueuer's call count.
    public class WalletServiceRateSyncTriggerTests
    {
        private readonly TransactionServiceTestFixture _fixture = new();

        [Fact]
        public async Task Add_EnqueuesARateSync_WhenTheWalletCurrencyHasNoRecentSnapshot()
        {
            (Account account, _, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(0m);
            // FakeRateService.HasRecentSnapshotAsync defaults to false for any currency not
            // explicitly marked — EGP is never marked here, simulating a brand-new currency.

            await _fixture.WalletService.Add(account.UserId.ToString(), new CreateWalletDto
            {
                Name = "Egypt Cash",
                WalletTypeId = 1,
                CurrencyCode = "EGP",
                ExcludeFromTotal = false,
            });

            Assert.Equal(1, _fixture.JobEnqueuer.EnqueueRateSyncCallCount);
        }

        [Fact]
        public async Task Add_DoesNotEnqueueARateSync_WhenTheWalletCurrencyAlreadyHasARecentSnapshot()
        {
            (Account account, _, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(0m);
            _fixture.RateService.MarkRecentSnapshot("EGP");

            await _fixture.WalletService.Add(account.UserId.ToString(), new CreateWalletDto
            {
                Name = "Egypt Cash",
                WalletTypeId = 1,
                CurrencyCode = "EGP",
                ExcludeFromTotal = false,
            });

            Assert.Equal(0, _fixture.JobEnqueuer.EnqueueRateSyncCallCount);
        }
    }
}
