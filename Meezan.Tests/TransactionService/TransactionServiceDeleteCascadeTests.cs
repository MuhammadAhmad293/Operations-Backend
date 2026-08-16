using Common.Dto;
using Meezan.DataModel.Entities;
using Meezan.Dto.DTOs.Transaction;

namespace Meezan.Tests.TransactionService
{
    // meezan-backend.md §7.3: fee cascade on parent transaction delete. BR-09: a fee is a
    // separate Expense Transaction (IsFee=true, linked via ParentTransaction), and the DB FK from
    // fee to parent is Restrict (not Cascade) — deleting the parent without deleting the fee first
    // would violate that constraint, so TransactionService.Delete finds and deletes linked fees
    // explicitly before deleting the parent (see the comment at TransactionService.cs:237).
    public class TransactionServiceDeleteCascadeTests
    {
        private readonly TransactionServiceTestFixture _fixture = new();

        public TransactionServiceDeleteCascadeTests()
        {
            _fixture.RateService.SetRate("SAR", "GOLD", 0.005m);
        }

        [Fact]
        public async Task Add_CreatesBothParentAndFeeTransactions_LinkedToEachOther()
        {
            (Account account, Wallet wallet, Category category) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);

            await AddExpenseWithFeeAsync(account, wallet, category, amount: 100m, feeAmount: 10m);

            List<Transaction> all = await _fixture.UnitOfWork.TransactionRepository.GetAllAsync();
            Assert.Equal(2, all.Count);

            Transaction parent = Assert.Single(all, t => !t.IsFee);
            Transaction fee = Assert.Single(all, t => t.IsFee);
            Assert.Equal(parent.Id, fee.ParentTransactionId);
            Assert.Equal(10m, fee.Amount);
        }

        [Fact]
        public async Task Delete_RemovesBothParentAndLinkedFeeTransaction()
        {
            (Account account, Wallet wallet, Category category) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);
            Transaction parent = await AddExpenseWithFeeAsync(account, wallet, category, amount: 100m, feeAmount: 10m);

            ResponseDto<EmptyResponseDto> deleteResult = await _fixture.TransactionService.Delete(account.UserId.ToString(), parent.Id);

            Assert.Equal(Common.Enums.ResponseStatus.Success, deleteResult.Status);
            List<Transaction> remaining = await _fixture.UnitOfWork.TransactionRepository.GetAllAsync();
            Assert.Empty(remaining);
        }

        [Fact]
        public async Task Delete_RestoresWalletBalance_ByTheFullParentPlusFeeAmount()
        {
            (Account account, Wallet wallet, Category category) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);
            Transaction parent = await AddExpenseWithFeeAsync(account, wallet, category, amount: 100m, feeAmount: 10m);

            decimal balanceAfterAdd = wallet.InitialAmount + await _fixture.UnitOfWork.TransactionRepository.GetSignedSumForWalletAsync(wallet.Id);
            Assert.Equal(890m, balanceAfterAdd); // 1000 - 100 - 10

            await _fixture.TransactionService.Delete(account.UserId.ToString(), parent.Id);

            decimal balanceAfterDelete = wallet.InitialAmount + await _fixture.UnitOfWork.TransactionRepository.GetSignedSumForWalletAsync(wallet.Id);
            Assert.Equal(1000m, balanceAfterDelete);
        }

        [Fact]
        public async Task Delete_OnlyRemovesTheTargetedTransactionsFee_LeavesOtherTransactionsUntouched()
        {
            (Account account, Wallet wallet, Category category) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);
            Transaction target = await AddExpenseWithFeeAsync(account, wallet, category, amount: 50m, feeAmount: 5m);
            Transaction other = await AddExpenseWithFeeAsync(account, wallet, category, amount: 30m, feeAmount: 3m);

            await _fixture.TransactionService.Delete(account.UserId.ToString(), target.Id);

            List<Transaction> remaining = await _fixture.UnitOfWork.TransactionRepository.GetAllAsync();
            Assert.Equal(2, remaining.Count); // other's parent + its own fee survive
            Assert.All(remaining, t => Assert.True(t.Id == other.Id || t.ParentTransactionId == other.Id));
        }

        private async Task<Transaction> AddExpenseWithFeeAsync(Account account, Wallet wallet, Category category, decimal amount, decimal feeAmount)
        {
            CreateTransactionDto dto = new()
            {
                Type = "Expense",
                DateGregorian = DateOnly.FromDateTime(DateTime.UtcNow),
                Time = TimeOnly.FromDateTime(DateTime.UtcNow),
                Amount = amount,
                WalletId = wallet.Id,
                CategoryId = category.Id,
                Fee = new CreateFeeDto { Amount = feeAmount },
            };

            await _fixture.TransactionService.Add(account.UserId.ToString(), dto);

            List<Transaction> all = await _fixture.UnitOfWork.TransactionRepository.GetAllAsync();
            return all.Where(t => !t.IsFee).OrderByDescending(t => t.Id).First();
        }
    }
}
