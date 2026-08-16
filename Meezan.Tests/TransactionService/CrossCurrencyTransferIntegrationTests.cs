using Meezan.DataModel.Entities;
using Meezan.Dto.DTOs.Transaction;

namespace Meezan.Tests.TransactionService
{
    // The same UC-14 acceptance example as CrossCurrencyConversionResolverTests, but run through
    // the real TransactionService.Add end-to-end (wallet resolution, balance math, persistence)
    // rather than the pure resolver in isolation — confirms the extraction in sub-task 6 didn't
    // change TransactionService's actual behavior.
    public class CrossCurrencyTransferIntegrationTests
    {
        private readonly TransactionServiceTestFixture _fixture = new();

        public CrossCurrencyTransferIntegrationTests()
        {
            // ZakatEngine.ReevaluateAsync (invoked inside Add) sums every non-excluded wallet's
            // gold-equivalent value regardless of its balance, so both currencies touched by the
            // transfer need a configured rate even though neither wallet is expected to cross nisab.
            _fixture.RateService.SetRate("SAR", "GOLD", 0.005m);
            _fixture.RateService.SetRate("USD", "GOLD", 0.005m);
        }

        [Fact]
        public async Task Add_AppliesAnOverriddenRate_ExactlyPerUC14sWorkedExample()
        {
            (Account account, Wallet _, Category _) = await _fixture.CreateAccountWithWalletAndCategoryAsync(sarBalance: 0m);

            Wallet usdWallet = new() { AccountId = account.Id, Name = "USD Wallet", WalletTypeId = 2, CurrencyCode = "USD", InitialAmount = 1000m, ExcludeFromTotal = false, IsArchived = false };
            _fixture.UnitOfWork.WalletRepository.Create(usdWallet);
            Wallet sarWallet = new() { AccountId = account.Id, Name = "SAR Wallet", WalletTypeId = 3, CurrencyCode = "SAR", InitialAmount = 0m, ExcludeFromTotal = false, IsArchived = false };
            _fixture.UnitOfWork.WalletRepository.Create(sarWallet);
            await _fixture.UnitOfWork.CommitAsync();

            CreateTransactionDto dto = new()
            {
                Type = "Transfer",
                DateGregorian = DateOnly.FromDateTime(DateTime.UtcNow),
                Time = TimeOnly.FromDateTime(DateTime.UtcNow),
                Amount = 1000m,
                WalletId = usdWallet.Id,
                ToWalletId = sarWallet.Id,
                ExchangeRate = 3.75m,
            };

            await _fixture.TransactionService.Add(account.UserId.ToString(), dto);

            Transaction transfer = (await _fixture.UnitOfWork.TransactionRepository.GetAllAsync()).Single();
            Assert.Equal(1000m, transfer.Amount);
            Assert.Equal(3.75m, transfer.ExchangeRate);
            Assert.Equal(3750m, transfer.ConvertedAmount);

            decimal usdBalance = usdWallet.InitialAmount + await _fixture.UnitOfWork.TransactionRepository.GetSignedSumForWalletAsync(usdWallet.Id);
            decimal sarBalance = sarWallet.InitialAmount + await _fixture.UnitOfWork.TransactionRepository.GetSignedSumForWalletAsync(sarWallet.Id);
            Assert.Equal(0m, usdBalance); // 1000 - 1000
            Assert.Equal(3750m, sarBalance); // 0 + 3750
        }
    }
}
