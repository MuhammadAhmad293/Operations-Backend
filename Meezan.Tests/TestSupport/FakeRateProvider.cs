using Meezan.IServices.IService;

namespace Meezan.Tests.TestSupport
{
    // Test double for IRateProvider — used to test RateService.SyncAsync's own persistence
    // behavior (append-only, never-update) in isolation from any real HTTP call. Returns whatever
    // quotes were configured, tagged with the current UtcNow so successive SyncAsync calls
    // produce distinguishably-timestamped rows.
    public class FakeRateProvider : IRateProvider
    {
        private readonly List<RateQuote> _fiatQuotes = new();
        private readonly Dictionary<string, RateQuote> _metalQuotes = new(StringComparer.OrdinalIgnoreCase);

        public FakeRateProvider AddFiatQuote(string fromCode, string toCode, decimal rate)
        {
            _fiatQuotes.Add(new RateQuote { FromCurrencyCode = fromCode, ToCurrencyCode = toCode, RatePerUnit = rate, FetchedAt = DateTime.UtcNow, Source = "Fake" });
            return this;
        }

        public FakeRateProvider SetMetalQuote(string metalCode, string normalizedFromCode, decimal ratePerGram)
        {
            _metalQuotes[metalCode] = new RateQuote { FromCurrencyCode = normalizedFromCode, ToCurrencyCode = "USD", RatePerUnit = ratePerGram * 31.1034768m, RatePerGram = ratePerGram, FetchedAt = DateTime.UtcNow, Source = "Fake" };
            return this;
        }

        public Task<List<RateQuote>> GetFiatRatesAsync(List<string> quoteCurrencyCodes, CancellationToken cancellationToken = default)
            => Task.FromResult(_fiatQuotes.Select(q => new RateQuote
            {
                FromCurrencyCode = q.FromCurrencyCode,
                ToCurrencyCode = q.ToCurrencyCode,
                RatePerUnit = q.RatePerUnit,
                RatePerGram = q.RatePerGram,
                FetchedAt = DateTime.UtcNow,
                Source = q.Source,
            }).ToList());

        public Task<RateQuote> GetMetalRateAsync(string metalCode, CancellationToken cancellationToken = default)
        {
            if (!_metalQuotes.TryGetValue(metalCode, out RateQuote? quote))
                throw new InvalidOperationException($"FakeRateProvider has no metal quote configured for {metalCode}.");

            return Task.FromResult(new RateQuote
            {
                FromCurrencyCode = quote.FromCurrencyCode,
                ToCurrencyCode = quote.ToCurrencyCode,
                RatePerUnit = quote.RatePerUnit,
                RatePerGram = quote.RatePerGram,
                FetchedAt = DateTime.UtcNow,
                Source = quote.Source,
            });
        }
    }
}
