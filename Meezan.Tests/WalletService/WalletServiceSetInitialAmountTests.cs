using Common.Enums;
using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;
using Meezan.Dto.DTOs.Wallet;
using Meezan.Services.CustomExceptions;
using Meezan.Tests.TransactionService;
using Microsoft.EntityFrameworkCore;

namespace Meezan.Tests.WalletService
{
    // Mode B ("Change initial amount", Phase 017): WalletService.SetInitialAmount corrects
    // Wallet.InitialAmount directly — no transaction, no history entry, retroactive by design.
    // Same real SQLite-backed fixture as Mode A's suite (real UnitOfWork/repositories/
    // ZakatEngine); only IRateService is a test double.
    public class WalletServiceSetInitialAmountTests
    {
        private readonly TransactionServiceTestFixture _fixture = new();

        public WalletServiceSetInitialAmountTests()
        {
            _fixture.RateService.SetRate("SAR", "GOLD", 0.005m); // 17,000 SAR == 85g nisab
        }

        [Fact]
        public async Task SetInitialAmount_IncreasesInitialAmountByTheDelta_WhenPositive()
        {
            (Account account, Wallet wallet, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);

            var response = await _fixture.WalletService.SetInitialAmount(account.UserId.ToString(),
                new SetWalletInitialAmountDto { Id = wallet.Id, NewBalance = 1500m });

            Assert.Equal(ResponseStatus.Success, response.Status);
            Wallet reloaded = await ReloadWalletAsync(wallet.Id);
            Assert.Equal(1500m, reloaded.InitialAmount); // no transactions yet, so InitialAmount *is* the balance
        }

        [Fact]
        public async Task SetInitialAmount_DecreasesInitialAmountByTheDelta_WhenNegative()
        {
            (Account account, Wallet wallet, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);

            await _fixture.WalletService.SetInitialAmount(account.UserId.ToString(),
                new SetWalletInitialAmountDto { Id = wallet.Id, NewBalance = 700m });

            Wallet reloaded = await ReloadWalletAsync(wallet.Id);
            Assert.Equal(700m, reloaded.InitialAmount);
        }

        [Fact]
        public async Task SetInitialAmount_AppliesTheDeltaOnTopOfExistingTransactionHistory()
        {
            (Account account, Wallet wallet, Category category) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);
            // A real -200 transaction (category is Expense-kind, per CreateAccountWithWalletAndCategoryAsync),
            // independent of the correction — SetInitialAmount must add the delta to InitialAmount,
            // not just set InitialAmount = newBalance.
            await _fixture.TransactionService.Add(account.UserId.ToString(), new Meezan.Dto.DTOs.Transaction.CreateTransactionDto
            {
                Type = "Expense",
                DateGregorian = DateOnly.FromDateTime(DateTime.UtcNow),
                Time = TimeOnly.FromDateTime(DateTime.UtcNow),
                Amount = 200m,
                WalletId = wallet.Id,
                CategoryId = category.Id,
            });
            // Current computed balance is now 800 (1000 InitialAmount - 200 transaction).

            await _fixture.WalletService.SetInitialAmount(account.UserId.ToString(),
                new SetWalletInitialAmountDto { Id = wallet.Id, NewBalance = 1500m }); // delta = +700

            Wallet reloaded = await ReloadWalletAsync(wallet.Id);
            Assert.Equal(1700m, reloaded.InitialAmount); // 1000 + 700, NOT 1500
            decimal recomputed = reloaded.InitialAmount + await _fixture.UnitOfWork.TransactionRepository.GetSignedSumForWalletAsync(wallet.Id);
            Assert.Equal(1500m, recomputed);
        }

        [Fact]
        public async Task SetInitialAmount_Throws_WhenDeltaIsZero()
        {
            (Account account, Wallet wallet, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);

            await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
                _fixture.WalletService.SetInitialAmount(account.UserId.ToString(),
                    new SetWalletInitialAmountDto { Id = wallet.Id, NewBalance = 1000m }));
        }

        [Fact]
        public async Task SetInitialAmount_CreatesNoTransaction()
        {
            (Account account, Wallet wallet, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);

            await _fixture.WalletService.SetInitialAmount(account.UserId.ToString(),
                new SetWalletInitialAmountDto { Id = wallet.Id, NewBalance = 1500m });

            Assert.False(await _fixture.Context.Set<Transaction>().AnyAsync(t => t.WalletId == wallet.Id));
        }

        [Fact]
        public async Task SetInitialAmount_UpdatesAGoldWalletsGrams_Directly()
        {
            (Account account, _, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 0m);
            Wallet goldWallet = await _fixture.AddWalletAsync(account, "GOLD", initialAmount: 10.000m);

            await _fixture.WalletService.SetInitialAmount(account.UserId.ToString(),
                new SetWalletInitialAmountDto { Id = goldWallet.Id, NewBalance = 12.500m });

            Wallet reloaded = await ReloadWalletAsync(goldWallet.Id);
            Assert.Equal(12.500m, reloaded.InitialAmount);
        }

        [Fact]
        public async Task SetInitialAmount_Throws_WhenWalletDoesNotExist()
        {
            (Account account, _, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);

            await Assert.ThrowsAsync<ObjectNotFoundException>(() =>
                _fixture.WalletService.SetInitialAmount(account.UserId.ToString(),
                    new SetWalletInitialAmountDto { Id = 999999, NewBalance = 1500m }));
        }

        [Fact]
        public async Task SetInitialAmount_Throws_WhenWalletBelongsToAnotherAccount()
        {
            (Account accountA, _, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);
            (_, Wallet walletB, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);

            await Assert.ThrowsAsync<ObjectNotFoundException>(() =>
                _fixture.WalletService.SetInitialAmount(accountA.UserId.ToString(),
                    new SetWalletInitialAmountDto { Id = walletB.Id, NewBalance = 1500m }));
        }

        [Fact]
        public async Task SetInitialAmount_Throws_WhenWalletIsArchived()
        {
            (Account account, Wallet wallet, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);
            wallet.IsArchived = true;
            _fixture.UnitOfWork.WalletRepository.Update(wallet);
            await _fixture.UnitOfWork.CommitAsync();

            await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
                _fixture.WalletService.SetInitialAmount(account.UserId.ToString(),
                    new SetWalletInitialAmountDto { Id = wallet.Id, NewBalance = 1500m }));
        }

        [Fact]
        public async Task SetInitialAmount_TriggersZakatReevaluation_AndCanStartAnActiveCycle()
        {
            (Account account, Wallet wallet, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 0m);

            await _fixture.WalletService.SetInitialAmount(account.UserId.ToString(),
                new SetWalletInitialAmountDto { Id = wallet.Id, NewBalance = 20000m }); // 100g at 0.005 -> above nisab

            ZakatCycle? active = await _fixture.UnitOfWork.ZakatCycleRepository.GetCurrentActiveByAccountAsync(account.Id);
            Assert.NotNull(active);
            Assert.Equal(ZakatCycleStatus.Active, active!.Status);
        }

        [Fact]
        public async Task SetInitialAmount_DoesNotChangeAnAlreadyDueCyclesFrozenFigures()
        {
            (Account account, Wallet wallet, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 20000m); // 100g
            ZakatCycle dueCycle = new()
            {
                AccountId = account.Id,
                HawlStartHijri = _fixture.Hijri.AddHijriYears(_fixture.Today, -1),
                HawlDueHijri = _fixture.Today,
                Status = ZakatCycleStatus.Due,
                PotGoldGramsAtDue = 100m,
                ZakatDueGoldGrams = 2.5m,
            };
            _fixture.UnitOfWork.ZakatCycleRepository.Create(dueCycle);
            await _fixture.UnitOfWork.CommitAsync();

            // A large correction — the pot changes dramatically, but only for whatever cycle is
            // currently Active, never for this already-Due one.
            await _fixture.WalletService.SetInitialAmount(account.UserId.ToString(),
                new SetWalletInitialAmountDto { Id = wallet.Id, NewBalance = 100000m });

            ZakatCycle reloaded = await _fixture.Context.Set<ZakatCycle>().SingleAsync(c => c.Id == dueCycle.Id);
            Assert.Equal(ZakatCycleStatus.Due, reloaded.Status);
            Assert.Equal(100m, reloaded.PotGoldGramsAtDue);
            Assert.Equal(2.5m, reloaded.ZakatDueGoldGrams);
        }

        private async Task<Wallet> ReloadWalletAsync(int walletId)
            => await _fixture.Context.Set<Wallet>().SingleAsync(w => w.Id == walletId);
    }
}
