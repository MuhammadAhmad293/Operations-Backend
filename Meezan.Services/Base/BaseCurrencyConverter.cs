using Meezan.DataModel.Entities;
using Meezan.IServices.IService;

namespace Meezan.Services.Base
{
    // Shared by every read-side aggregate that needs to convert a transaction's wallet-currency
    // Amount into the account's base currency (Overview/Calendar/Statistics — Phase 011).
    // Instantiated once per request; caches one IRateService lookup per distinct wallet currency
    // so a request touching many same-currency transactions doesn't repeat rate lookups.
    public class BaseCurrencyConverter
    {
        private readonly Account _account;
        private readonly Dictionary<int, Wallet> _walletsById;
        private readonly IRateService _rateService;
        private readonly Dictionary<string, decimal> _rateCache = new(StringComparer.OrdinalIgnoreCase);

        public BaseCurrencyConverter(Account account, List<Wallet> wallets, IRateService rateService)
        {
            _account = account;
            _walletsById = wallets.ToDictionary(w => w.Id);
            _rateService = rateService;
        }

        // Converts one transaction's Amount from its source wallet's currency into the
        // account's base currency at the latest rate.
        public Task<decimal> ConvertAsync(Transaction transaction, CancellationToken cancellationToken)
        {
            Wallet wallet = _walletsById[transaction.WalletId];
            return ConvertAmountAsync(transaction.Amount, wallet.CurrencyCode, cancellationToken);
        }

        // Lower-level variant for callers converting something other than a transaction's own
        // Amount (e.g. StatisticsService converting a computed wallet *balance*) — same rate
        // cache, same account base-currency target.
        public async Task<decimal> ConvertAmountAsync(decimal amount, string currencyCode, CancellationToken cancellationToken)
        {
            decimal rate = await GetRateAsync(currencyCode, cancellationToken);
            return amount * rate;
        }

        private async Task<decimal> GetRateAsync(string currencyCode, CancellationToken cancellationToken)
        {
            if (_rateCache.TryGetValue(currencyCode, out decimal cached))
                return cached;

            decimal rate = await _rateService.GetLatestAsync(currencyCode, _account.BaseCurrencyCode, cancellationToken);
            _rateCache[currencyCode] = rate;
            return rate;
        }
    }
}
