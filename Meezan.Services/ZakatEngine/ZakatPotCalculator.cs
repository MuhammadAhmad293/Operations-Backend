using Meezan.DataModel.Entities;
using Meezan.IRepositories.UnitOfWork;
using Meezan.IServices.IService;

namespace Meezan.Services.ZakatEngine
{
    public class ZakatPotCalculator : IZakatPotCalculator
    {
        // BR-14/BR-15 constants — centralized here (rather than duplicated in ZakatEngine and
        // ZakatService) since both need the identical values and drift between them would be a
        // real correctness bug, not just a style inconsistency.
        public const decimal NisabGoldGrams = 85m;
        public const decimal ZakatRate = 0.025m;

        private const string GoldCurrencyCode = "GOLD";

        private IUnitOfWork UnitOfWork { get; }
        private IRateService RateService { get; }

        public ZakatPotCalculator(IUnitOfWork unitOfWork, IRateService rateService)
        {
            UnitOfWork = unitOfWork;
            RateService = rateService;
        }

        public async Task<decimal> ComputePotGoldGramsAsync(int accountId, CancellationToken cancellationToken = default)
        {
            List<Wallet> includedWallets = await UnitOfWork.WalletRepository.GetIncludedWalletsByAccountAsync(accountId);

            Dictionary<string, decimal> rateToGoldCache = new(StringComparer.OrdinalIgnoreCase);
            async Task<decimal> GetRateToGoldAsync(string currencyCode)
            {
                if (string.Equals(currencyCode, GoldCurrencyCode, StringComparison.OrdinalIgnoreCase))
                    return 1m;

                if (rateToGoldCache.TryGetValue(currencyCode, out decimal cached))
                    return cached;

                decimal rate = await RateService.GetLatestAsync(currencyCode, GoldCurrencyCode, cancellationToken);
                rateToGoldCache[currencyCode] = rate;
                return rate;
            }

            decimal totalGoldGrams = 0m;
            foreach (Wallet wallet in includedWallets)
            {
                if (string.Equals(wallet.CurrencyCode, GoldCurrencyCode, StringComparison.OrdinalIgnoreCase))
                {
                    // BR-03: pure-24K equivalent, not the raw purchased grams the wallet screen shows.
                    totalGoldGrams += await UnitOfWork.TransactionRepository.GetSignedPureGoldGramsSumForWalletAsync(wallet.Id);
                }
                else
                {
                    decimal balance = wallet.InitialAmount + await UnitOfWork.TransactionRepository.GetSignedSumForWalletAsync(wallet.Id);
                    decimal rate = await GetRateToGoldAsync(wallet.CurrencyCode);
                    totalGoldGrams += balance * rate;
                }
            }

            return totalGoldGrams;
        }
    }
}
