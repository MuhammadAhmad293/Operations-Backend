using Common.Enums;
using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;
using Meezan.Dto.DTOs.Transaction;
using Meezan.Dto.DTOs.Wallet;
using Meezan.Services.CustomExceptions;
using Meezan.Tests.TransactionService;
using Microsoft.EntityFrameworkCore;

namespace Meezan.Tests.WalletService
{
    // Mode A ("Adjust by transaction", Phase 017): WalletService.AdjustBalance posts a real
    // Income/Expense transaction for the delta, through the same TransactionService path every
    // other transaction uses. Exercised against the real SQLite-backed fixture (real UnitOfWork/
    // repositories/relational transactions, real ZakatEngine/GoldPurityCalculator) — only
    // IRateService is a test double, since rate integration is its own suite's concern.
    public class WalletServiceAdjustBalanceTests
    {
        private readonly TransactionServiceTestFixture _fixture = new();

        public WalletServiceAdjustBalanceTests()
        {
            // Every transaction write triggers ZakatEngine.ReevaluateAsync, which needs a
            // SAR->GOLD rate to value any non-gold wallet toward the pot — same setup every
            // other TransactionServiceTestFixture-based suite already uses.
            _fixture.RateService.SetRate("SAR", "GOLD", 0.005m); // 17,000 SAR == 85g nisab
        }

        [Fact]
        public async Task AdjustBalance_CreatesAnIncomeTransaction_WhenDeltaIsPositive()
        {
            (Account account, Wallet wallet, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);

            var response = await _fixture.WalletService.AdjustBalance(account.UserId.ToString(),
                new AdjustWalletBalanceDto { Id = wallet.Id, NewBalance = 1500m });

            Assert.Equal(ResponseStatus.Success, response.Status);
            Transaction transaction = await SingleAdjustmentTransactionAsync(wallet.Id);
            Assert.Equal(TransactionType.Income, transaction.Type);
            Assert.Equal(500m, transaction.Amount);
        }

        [Fact]
        public async Task AdjustBalance_CreatesAnExpenseTransaction_WhenDeltaIsNegative()
        {
            (Account account, Wallet wallet, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);

            await _fixture.WalletService.AdjustBalance(account.UserId.ToString(),
                new AdjustWalletBalanceDto { Id = wallet.Id, NewBalance = 700m });

            Transaction transaction = await SingleAdjustmentTransactionAsync(wallet.Id);
            Assert.Equal(TransactionType.Expense, transaction.Type);
            Assert.Equal(300m, transaction.Amount);
        }

        [Fact]
        public async Task AdjustBalance_Throws_WhenDeltaIsZero()
        {
            (Account account, Wallet wallet, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);

            await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
                _fixture.WalletService.AdjustBalance(account.UserId.ToString(),
                    new AdjustWalletBalanceDto { Id = wallet.Id, NewBalance = 1000m }));

            Assert.False(await _fixture.Context.Set<Transaction>().AnyAsync(t => t.WalletId == wallet.Id));
        }

        [Fact]
        public async Task AdjustBalance_SetsKaratAndPureGoldGrams_ForAGoldWallet()
        {
            (Account account, _, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 0m);
            Wallet goldWallet = await _fixture.AddWalletAsync(account, "GOLD", initialAmount: 10.000m);

            await _fixture.WalletService.AdjustBalance(account.UserId.ToString(),
                new AdjustWalletBalanceDto { Id = goldWallet.Id, NewBalance = 15.000m });

            Transaction transaction = await SingleAdjustmentTransactionAsync(goldWallet.Id);
            Assert.Equal(5.000m, transaction.Amount);
            Assert.Equal(24, transaction.Karat);
            Assert.Equal(5.000m, transaction.PureGoldGrams); // 24K: pure grams == the delta itself
        }

        [Theory]
        [InlineData(1500)]
        [InlineData(700)]
        public async Task AdjustBalance_RecomputedBalance_EqualsNewBalance(decimal newBalance)
        {
            (Account account, Wallet wallet, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);

            await _fixture.WalletService.AdjustBalance(account.UserId.ToString(),
                new AdjustWalletBalanceDto { Id = wallet.Id, NewBalance = newBalance });

            List<Meezan.DataModel.Entities.Wallet> wallets = await _fixture.UnitOfWork.WalletRepository.GetByAccountAsync(account.Id);
            Wallet reloaded = wallets.Single(w => w.Id == wallet.Id);
            decimal recomputed = reloaded.InitialAmount + await _fixture.UnitOfWork.TransactionRepository.GetSignedSumForWalletAsync(wallet.Id);
            Assert.Equal(newBalance, recomputed);
        }

        [Fact]
        public async Task AdjustBalance_SetsIsAdjustmentTrue_OnTheCreatedTransaction()
        {
            (Account account, Wallet wallet, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);

            await _fixture.WalletService.AdjustBalance(account.UserId.ToString(),
                new AdjustWalletBalanceDto { Id = wallet.Id, NewBalance = 1500m });

            Transaction transaction = await SingleAdjustmentTransactionAsync(wallet.Id);
            Assert.True(transaction.IsAdjustment);

            Category category = await _fixture.Context.Set<Category>().SingleAsync(c => c.Id == transaction.CategoryId);
            Assert.Equal(CategorySystemPurpose.BalanceAdjustment, category.SystemPurpose);
            Assert.True(category.IsProtected);
        }

        [Fact]
        public async Task AdjustBalance_CreatesTheCategoryLazily_ForAnAccountThatPredatesThisFeature()
        {
            // CreateAccountWithWalletAndCategoryAsync seeds only an ordinary category — no
            // Balance Adjustment row exists yet, simulating an account created before this
            // feature shipped.
            (Account account, Wallet wallet, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);
            Assert.False(await _fixture.Context.Set<Category>().AnyAsync(c => c.SystemPurpose == CategorySystemPurpose.BalanceAdjustment));

            await _fixture.WalletService.AdjustBalance(account.UserId.ToString(),
                new AdjustWalletBalanceDto { Id = wallet.Id, NewBalance = 1500m });

            Category created = await _fixture.Context.Set<Category>().SingleAsync(c => c.SystemPurpose == CategorySystemPurpose.BalanceAdjustment);
            Assert.Equal(CategoryKind.Income, created.Kind);
        }

        [Fact]
        public async Task AdjustBalance_ReusesTheSameCategory_OnASecondAdjustmentOfTheSameKind()
        {
            (Account account, Wallet wallet, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);

            await _fixture.WalletService.AdjustBalance(account.UserId.ToString(),
                new AdjustWalletBalanceDto { Id = wallet.Id, NewBalance = 1500m }); // +500, Income
            await _fixture.WalletService.AdjustBalance(account.UserId.ToString(),
                new AdjustWalletBalanceDto { Id = wallet.Id, NewBalance = 1800m }); // +300, Income again

            int incomeCategoryCount = await _fixture.Context.Set<Category>()
                .CountAsync(c => c.SystemPurpose == CategorySystemPurpose.BalanceAdjustment && c.Kind == CategoryKind.Income);
            Assert.Equal(1, incomeCategoryCount);
        }

        [Fact]
        public async Task AdjustBalance_Throws_WhenWalletDoesNotExist()
        {
            (Account account, _, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);

            await Assert.ThrowsAsync<ObjectNotFoundException>(() =>
                _fixture.WalletService.AdjustBalance(account.UserId.ToString(),
                    new AdjustWalletBalanceDto { Id = 999999, NewBalance = 1500m }));
        }

        [Fact]
        public async Task AdjustBalance_Throws_WhenWalletBelongsToAnotherAccount()
        {
            (Account accountA, _, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);
            (_, Wallet walletB, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);

            await Assert.ThrowsAsync<ObjectNotFoundException>(() =>
                _fixture.WalletService.AdjustBalance(accountA.UserId.ToString(),
                    new AdjustWalletBalanceDto { Id = walletB.Id, NewBalance = 1500m }));
        }

        [Fact]
        public async Task AdjustBalance_Throws_WhenWalletIsSoftDeleted()
        {
            (Account account, Wallet wallet, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);
            wallet.IsDeleted = true;
            _fixture.UnitOfWork.WalletRepository.Update(wallet);
            await _fixture.UnitOfWork.CommitAsync();

            await Assert.ThrowsAsync<ObjectNotFoundException>(() =>
                _fixture.WalletService.AdjustBalance(account.UserId.ToString(),
                    new AdjustWalletBalanceDto { Id = wallet.Id, NewBalance = 1500m }));
        }

        [Fact]
        public async Task AdjustBalance_Throws_WhenWalletIsArchived()
        {
            (Account account, Wallet wallet, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);
            wallet.IsArchived = true;
            _fixture.UnitOfWork.WalletRepository.Update(wallet);
            await _fixture.UnitOfWork.CommitAsync();

            await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
                _fixture.WalletService.AdjustBalance(account.UserId.ToString(),
                    new AdjustWalletBalanceDto { Id = wallet.Id, NewBalance = 1500m }));
        }

        [Fact]
        public async Task AdjustBalance_TriggersZakatReevaluation_AndCanStartAnActiveCycle()
        {
            (Account account, Wallet wallet, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 0m);

            await _fixture.WalletService.AdjustBalance(account.UserId.ToString(),
                new AdjustWalletBalanceDto { Id = wallet.Id, NewBalance = 20000m }); // 100g at 0.005 -> above 85g nisab

            ZakatCycle? active = await _fixture.UnitOfWork.ZakatCycleRepository.GetCurrentActiveByAccountAsync(account.Id);
            Assert.NotNull(active);
            Assert.Equal(ZakatCycleStatus.Active, active!.Status);
        }

        [Fact]
        public async Task Update_AllowsRecategorizingAnAdjustmentTransaction_AwayFromTheProtectedCategory_WithoutError()
        {
            (Account account, Wallet wallet, Category ordinaryExpenseCategory) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);
            await _fixture.WalletService.AdjustBalance(account.UserId.ToString(),
                new AdjustWalletBalanceDto { Id = wallet.Id, NewBalance = 700m }); // -300, Expense
            Transaction adjustment = await SingleAdjustmentTransactionAsync(wallet.Id);

            await _fixture.TransactionService.Update(account.UserId.ToString(), new UpdateTransactionDto
            {
                Id = adjustment.Id,
                Type = "Expense",
                DateGregorian = adjustment.DateGregorian,
                Time = adjustment.Time,
                Amount = adjustment.Amount,
                WalletId = wallet.Id,
                CategoryId = ordinaryExpenseCategory.Id, // recategorized away from Balance Adjustment
            });

            Transaction reloaded = await _fixture.Context.Set<Transaction>().SingleAsync(t => t.Id == adjustment.Id);
            Assert.Equal(ordinaryExpenseCategory.Id, reloaded.CategoryId);
            Assert.True(reloaded.IsAdjustment); // immutable historical fact, mirrors IsFee
        }

        [Fact]
        public async Task Add_AllowsExplicitlySelectingTheProtectedCategory_OnAnOrdinaryTransaction_WithoutError()
        {
            (Account account, Wallet wallet, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);
            await _fixture.WalletService.AdjustBalance(account.UserId.ToString(),
                new AdjustWalletBalanceDto { Id = wallet.Id, NewBalance = 1500m }); // creates the Income Balance Adjustment category
            Category adjustmentCategory = await _fixture.Context.Set<Category>().SingleAsync(c => c.SystemPurpose == CategorySystemPurpose.BalanceAdjustment && c.Kind == CategoryKind.Income);

            var response = await _fixture.TransactionService.Add(account.UserId.ToString(), new CreateTransactionDto
            {
                Type = "Income",
                DateGregorian = DateOnly.FromDateTime(DateTime.UtcNow),
                Time = TimeOnly.FromDateTime(DateTime.UtcNow),
                Amount = 50m,
                WalletId = wallet.Id,
                CategoryId = adjustmentCategory.Id, // never done by real UI, but the backend doesn't forbid it either
            });

            Assert.Equal(ResponseStatus.Success, response.Status);
            Transaction created = await _fixture.Context.Set<Transaction>().SingleAsync(t => t.Amount == 50m && t.WalletId == wallet.Id);
            Assert.False(created.IsAdjustment); // only AddAdjustment ever sets this flag
        }

        private async Task<Transaction> SingleAdjustmentTransactionAsync(int walletId)
            => await _fixture.Context.Set<Transaction>().SingleAsync(t => t.WalletId == walletId && t.IsAdjustment);
    }
}
