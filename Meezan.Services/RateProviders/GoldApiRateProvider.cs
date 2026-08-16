using Meezan.IServices.IService;
using Meezan.Services.Setting;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Meezan.Services.RateProviders
{
    // Metals-only fallback adapter for gold-api.com (spec §7) — used by CompositeRateProvider
    // (sub-task 5) only when Frankfurter's metal quote fails. Same XAU→GOLD/XAG→SILVER
    // translation boundary as FrankfurterRateProvider; fiat quotes are out of scope for this
    // provider entirely (gold-api.com has no FX endpoint), so GetFiatRatesAsync is unsupported.
    public class GoldApiRateProvider : IRateProvider
    {
        private const decimal TroyOunceToGrams = 31.1034768m;
        private const string SourceName = "GoldApi";

        private static readonly Dictionary<string, string> MetalCodeTranslation = new(StringComparer.OrdinalIgnoreCase)
        {
            ["XAU"] = "GOLD",
            ["XAG"] = "SILVER",
        };

        private readonly HttpClient _httpClient;
        private readonly RateIntegrationSettings _settings;

        public GoldApiRateProvider(HttpClient httpClient, RateIntegrationSettings settings)
        {
            _httpClient = httpClient;
            _settings = settings;
        }

        public Task<List<RateQuote>> GetFiatRatesAsync(List<string> quoteCurrencyCodes, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("gold-api.com is a metals-only fallback and does not provide FX rates.");

        public async Task<RateQuote> GetMetalRateAsync(string metalCode, CancellationToken cancellationToken = default)
        {
            string url = $"{_settings.GoldApi.BaseUrl}/price/{metalCode}";

            GoldApiPriceResponse response = await _httpClient.GetFromJsonAsync<GoldApiPriceResponse>(url, cancellationToken)
                ?? throw new HttpRequestException($"gold-api.com returned an empty response for metal code {metalCode}.");

            string normalizedCode = MetalCodeTranslation.TryGetValue(metalCode, out string? mapped) ? mapped : metalCode;

            return new RateQuote
            {
                FromCurrencyCode = normalizedCode,
                ToCurrencyCode = response.Currency,
                RatePerUnit = response.Price,
                RatePerGram = response.Price / TroyOunceToGrams,
                FetchedAt = DateTime.UtcNow,
                Source = SourceName,
            };
        }

        // gold-api.com's GET /price/{symbol} returns a single object, price = USD per troy
        // ounce (verified live: {"currency":"USD","exchangeRate":1.0,"name":"Gold","price":
        // 4343.29,"symbol":"XAU","updatedAt":"...","updatedAtReadable":"..."}). Unknown symbols
        // return 404 {"error":"Symbol not found"} — GetFromJsonAsync throws on non-2xx already.
        private class GoldApiPriceResponse
        {
            [JsonPropertyName("currency")]
            public string Currency { get; set; }

            [JsonPropertyName("price")]
            public decimal Price { get; set; }

            [JsonPropertyName("symbol")]
            public string Symbol { get; set; }

            [JsonPropertyName("updatedAt")]
            public string UpdatedAt { get; set; }
        }
    }
}
