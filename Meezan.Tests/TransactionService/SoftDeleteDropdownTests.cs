using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;
using Meezan.Dto.DTOs.Category;
using Meezan.Dto.DTOs.Transaction;
using Meezan.Dto.DTOs.Wallet;

namespace Meezan.Tests.TransactionService
{
    // meezan-backend.md §7.4 / BR-06: deleting a wallet or category referenced by transactions is
    // a soft delete (IsDeleted=true) — historical transactions keep resolving its name, but it
    // disappears from the dropdown list, and once a transaction's selection moves off it, it can
    // never be picked again (a consequence of staying excluded, not a separate rule). Unreferenced
    // wallets/categories are hard-deleted outright.
    public class SoftDeleteDropdownTests
    {
        private readonly TransactionServiceTestFixture _fixture = new();

        public SoftDeleteDropdownTests()
        {
            _fixture.RateService.SetRate("SAR", "GOLD", 0.005m);
        }

        [Fact]
        public async Task Wallet_Delete_HardDeletes_WhenUnreferencedByAnyTransaction()
        {
            (Account account, Wallet wallet, _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);

            await _fixture.WalletService.Delete(account.UserId.ToString(), wallet.Id);

            List<Wallet> including = await _fixture.UnitOfWork.WalletRepository.GetByAccountIncludingDeletedAsync(account.Id);
            Assert.DoesNotContain(including, w => w.Id == wallet.Id);
        }

        [Fact]
        public async Task Wallet_Delete_SoftDeletes_WhenReferencedByATransaction()
        {
            (Account account, Wallet wallet, Category category) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);
            await AddIncomeAsync(account, wallet, category, 100m);

            await _fixture.WalletService.Delete(account.UserId.ToString(), wallet.Id);

            List<Wallet> including = await _fixture.UnitOfWork.WalletRepository.GetByAccountIncludingDeletedAsync(account.Id);
            Wallet stored = Assert.Single(including, w => w.Id == wallet.Id);
            Assert.True(stored.IsDeleted);
        }

        [Fact]
        public async Task Wallet_GetAll_ExcludesASoftDeletedWallet_FromTheDropdown()
        {
            (Account account, Wallet wallet, Category category) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);
            await AddIncomeAsync(account, wallet, category, 100m);
            await _fixture.WalletService.Delete(account.UserId.ToString(), wallet.Id);

            var response = await _fixture.WalletService.GetAll(account.UserId.ToString());

            Assert.DoesNotContain(response.Data, (WalletDto w) => w.Id == wallet.Id);
        }

        [Fact]
        public async Task Transaction_GetById_StillResolvesASoftDeletedWalletsName()
        {
            (Account account, Wallet wallet, Category category) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);
            Transaction transaction = await AddIncomeAsync(account, wallet, category, 100m);
            await _fixture.WalletService.Delete(account.UserId.ToString(), wallet.Id);

            var response = await _fixture.TransactionService.GetById(account.UserId.ToString(), transaction.Id);

            Assert.Equal("Cash", response.Data.WalletName);
        }

        [Fact]
        public async Task Category_Delete_HardDeletes_WhenUnreferencedAndChildless()
        {
            (Account account, _, Category category) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);

            await _fixture.CategoryService.Delete(account.UserId.ToString(), category.Id);

            List<Category> including = await _fixture.UnitOfWork.CategoryRepository.GetByKindIncludingDeletedAsync(account.Id, CategoryKind.Expense);
            Assert.DoesNotContain(including, c => c.Id == category.Id);
        }

        [Fact]
        public async Task Category_Delete_SoftDeletes_WhenReferencedByATransaction()
        {
            (Account account, Wallet wallet, Category category) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);
            await AddExpenseAsync(account, wallet, category, 50m);

            await _fixture.CategoryService.Delete(account.UserId.ToString(), category.Id);

            List<Category> including = await _fixture.UnitOfWork.CategoryRepository.GetByKindIncludingDeletedAsync(account.Id, CategoryKind.Expense);
            Category stored = Assert.Single(including, c => c.Id == category.Id);
            Assert.True(stored.IsDeleted);
        }

        [Fact]
        public async Task Category_Delete_SoftDeletes_WhenItHasChildren_EvenWithNoTransactions()
        {
            (Account account, _, Category parent) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);
            Category child = new() { AccountId = account.Id, ParentId = parent.Id, Kind = CategoryKind.Expense, Name = "Child", SortOrder = 0, IsProtected = false };
            _fixture.UnitOfWork.CategoryRepository.Create(child);
            await _fixture.UnitOfWork.CommitAsync();

            await _fixture.CategoryService.Delete(account.UserId.ToString(), parent.Id);

            List<Category> including = await _fixture.UnitOfWork.CategoryRepository.GetByKindIncludingDeletedAsync(account.Id, CategoryKind.Expense);
            Category stored = Assert.Single(including, c => c.Id == parent.Id);
            Assert.True(stored.IsDeleted);
        }

        [Fact]
        public async Task Category_GetTree_ExcludesASoftDeletedCategory_FromTheDropdown()
        {
            (Account account, Wallet wallet, Category category) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);
            await AddExpenseAsync(account, wallet, category, 50m);
            await _fixture.CategoryService.Delete(account.UserId.ToString(), category.Id);

            var response = await _fixture.CategoryService.GetTree(account.UserId.ToString(), "Expense");

            Assert.DoesNotContain(response.Data, (CategoryDto c) => c.Id == category.Id);
        }

        [Fact]
        public async Task Transaction_GetById_StillResolvesASoftDeletedCategorysName()
        {
            (Account account, Wallet wallet, Category category) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);
            Transaction transaction = await AddExpenseAsync(account, wallet, category, 50m);
            await _fixture.CategoryService.Delete(account.UserId.ToString(), category.Id);

            var response = await _fixture.TransactionService.GetById(account.UserId.ToString(), transaction.Id);

            Assert.Equal("Test Expense", response.Data.CategoryName);
        }

        [Fact]
        public async Task Wallet_OnceReassignedAwayFromASoftDeletedWallet_ThatWalletCanNeverBeSelectedAgain()
        {
            (Account account, Wallet oldWallet, Category category) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 1000m);
            Transaction transaction = await AddIncomeAsync(account, oldWallet, category, 100m);
            await _fixture.WalletService.Delete(account.UserId.ToString(), oldWallet.Id);

            Wallet newWallet = new() { AccountId = account.Id, Name = "New Wallet", WalletTypeId = 3, CurrencyCode = "SAR", InitialAmount = 0m, ExcludeFromTotal = false, IsArchived = false };
            _fixture.UnitOfWork.WalletRepository.Create(newWallet);
            await _fixture.UnitOfWork.CommitAsync();

            UpdateTransactionDto updateDto = new()
            {
                Id = transaction.Id,
                Type = "Income",
                DateGregorian = transaction.DateGregorian,
                Time = transaction.Time,
                Amount = 100m,
                WalletId = newWallet.Id,
                CategoryId = transaction.CategoryId, // the Income category AddIncomeAsync created
            };
            await _fixture.TransactionService.Update(account.UserId.ToString(), updateDto);

            var dropdown = await _fixture.WalletService.GetAll(account.UserId.ToString());
            Assert.DoesNotContain(dropdown.Data, (WalletDto w) => w.Id == oldWallet.Id);

            var updated = await _fixture.TransactionService.GetById(account.UserId.ToString(), transaction.Id);
            Assert.Equal("New Wallet", updated.Data.WalletName);
        }

        private async Task<Transaction> AddIncomeAsync(Account account, Wallet wallet, Category expenseCategory, decimal amount)
        {
            Category income = new() { AccountId = account.Id, Kind = CategoryKind.Income, Name = "Test Income", SortOrder = 0, IsProtected = false };
            _fixture.UnitOfWork.CategoryRepository.Create(income);
            await _fixture.UnitOfWork.CommitAsync();

            CreateTransactionDto dto = new()
            {
                Type = "Income",
                DateGregorian = DateOnly.FromDateTime(DateTime.UtcNow),
                Time = TimeOnly.FromDateTime(DateTime.UtcNow),
                Amount = amount,
                WalletId = wallet.Id,
                CategoryId = income.Id,
            };
            await _fixture.TransactionService.Add(account.UserId.ToString(), dto);

            List<Transaction> all = await _fixture.UnitOfWork.TransactionRepository.GetAllAsync();
            return all.Where(t => t.WalletId == wallet.Id && t.Type == TransactionType.Income).OrderByDescending(t => t.Id).First();
        }

        private async Task<Transaction> AddExpenseAsync(Account account, Wallet wallet, Category expenseCategory, decimal amount)
        {
            CreateTransactionDto dto = new()
            {
                Type = "Expense",
                DateGregorian = DateOnly.FromDateTime(DateTime.UtcNow),
                Time = TimeOnly.FromDateTime(DateTime.UtcNow),
                Amount = amount,
                WalletId = wallet.Id,
                CategoryId = expenseCategory.Id,
            };
            await _fixture.TransactionService.Add(account.UserId.ToString(), dto);

            List<Transaction> all = await _fixture.UnitOfWork.TransactionRepository.GetAllAsync();
            return all.Where(t => t.WalletId == wallet.Id && t.Type == TransactionType.Expense).OrderByDescending(t => t.Id).First();
        }
    }
}
