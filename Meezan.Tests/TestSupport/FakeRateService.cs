using Common.Dto;
using Meezan.Dto.DTOs.Rate;
using Meezan.IServices.IService;

namespace Meezan.Tests.TestSupport
{
    // Test double for IRateService — only GetLatestAsync is meaningfully implemented;
    // SyncAsync/GetLatestQuotesAsync are out of scope for every suite using this fake
    // (rate-integration behavior is sub-task 7's job).
    public class FakeRateService : IRateService
    {
        private readonly Dictionary<(string From, string To), decimal> _rates = new();
        private readonly HashSet<string> _currenciesWithRecentSnapshots = new(StringComparer.OrdinalIgnoreCase);

        public FakeRateService SetRate(string from, string to, decimal rate)
        {
            _rates[(from.ToUpperInvariant(), to.ToUpperInvariant())] = rate;
            return this;
        }

        public FakeRateService MarkRecentSnapshot(string currencyCode)
        {
            _currenciesWithRecentSnapshots.Add(currencyCode);
            return this;
        }

        public Task<bool> HasRecentSnapshotAsync(string currencyCode, CancellationToken cancellationToken = default)
            => Task.FromResult(_currenciesWithRecentSnapshots.Contains(currencyCode));

        public Task<decimal> GetLatestAsync(string fromCurrencyCode, string toCurrencyCode, CancellationToken cancellationToken = default)
        {
            if (string.Equals(fromCurrencyCode, toCurrencyCode, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(1m);

            string from = fromCurrencyCode.ToUpperInvariant();
            string to = toCurrencyCode.ToUpperInvariant();

            if (_rates.TryGetValue((from, to), out decimal rate))
                return Task.FromResult(rate);

            if (_rates.TryGetValue((to, from), out decimal inverse))
                return Task.FromResult(1m / inverse);

            throw new InvalidOperationException($"FakeRateService has no rate configured for {fromCurrencyCode}->{toCurrencyCode}");
        }

        public Task SyncAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not used by these tests.");

        public Task<ResponseDto<RatesResponseDto>> GetLatestQuotesAsync(string baseCurrencyCode, List<string> quoteCurrencyCodes, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not used by these tests.");
    }
}
