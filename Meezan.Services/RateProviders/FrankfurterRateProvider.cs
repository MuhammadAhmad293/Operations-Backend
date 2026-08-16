using Meezan.IServices.IService;
using Meezan.Services.Setting;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Meezan.Services.RateProviders
{
    // Adapter for Frankfurter API v2 (spec §7) — USD-pivot only, per the plan's Open-Question
    // Resolution (keeps the call count constant regardless of how many currencies the
    // `Currencies` lookup table grows to via the currency-sync in sub-task 7). Translates the
    // provider's native metal codes (XAU/XAG) to this system's Currency codes (GOLD/SILVER) at
    // this boundary — nothing downstream ever sees XAU/XAG.
    public class FrankfurterRateProvider : IRateProvider
    {
        private const decimal TroyOunceToGrams = 31.1034768m;
        private const string SourceName = "Frankfurter";

        private static readonly Dictionary<string, string> MetalCodeTranslation = new(StringComparer.OrdinalIgnoreCase)
        {
            ["XAU"] = "GOLD",
            ["XAG"] = "SILVER",
        };

        private static readonly Dictionary<string, string> ProviderCodeTranslation = new(StringComparer.OrdinalIgnoreCase)
        {
            ["GOLD"] = "XAU",
            ["SILVER"] = "XAG",
        };

        private readonly HttpClient _httpClient;
        private readonly RateIntegrationSettings _settings;

        public FrankfurterRateProvider(HttpClient httpClient, RateIntegrationSettings settings)
        {
            _httpClient = httpClient;
            _settings = settings;
        }

        public async Task<List<RateQuote>> GetFiatRatesAsync(List<string> quoteCurrencyCodes, CancellationToken cancellationToken = default)
        {
            if (quoteCurrencyCodes.Count == 0)
                return new List<RateQuote>();

            string quotes = string.Join(",", quoteCurrencyCodes);
            string url = $"{_settings.Frankfurter.BaseUrl}/rates?base=USD&quotes={quotes}";

            List<FrankfurterRateEntry> entries = await _httpClient.GetFromJsonAsync<List<FrankfurterRateEntry>>(url, cancellationToken)
                ?? throw new HttpRequestException("Frankfurter returned an empty response for the FX quote request.");

            DateTime fetchedAt = DateTime.UtcNow;

            return entries.Select(entry => new RateQuote
            {
                FromCurrencyCode = "USD",
                ToCurrencyCode = entry.Quote,
                RatePerUnit = entry.Rate,
                RatePerGram = null,
                FetchedAt = fetchedAt,
                Source = SourceName,
            }).ToList();
        }

        // Direct (non-USD-anchored) pairs for currencies actually in use (Phase 016 enhancement).
        // Frankfurter's /rates endpoint accepts *any* currency as `base` — including XAU/XAG,
        // already relied on by GetMetalRateAsync above — and metals mixed into `quotes` right
        // alongside fiat codes; live-verified with base=SAR&quotes=EGP,XAU,XAG,USD returning all
        // four in one response. That lets one batched call per in-use base currency produce every
        // direct pair against the other in-use currencies and against GOLD/SILVER at once, instead
        // of one call per pair.
        public async Task<List<RateQuote>> GetDirectRatesAsync(string baseCurrencyCode, List<string> quoteCurrencyCodes, CancellationToken cancellationToken = default)
        {
            if (quoteCurrencyCodes.Count == 0)
                return new List<RateQuote>();

            bool baseIsMetal = ProviderCodeTranslation.ContainsKey(baseCurrencyCode);
            string apiBase = ToProviderCode(baseCurrencyCode);
            string quotesCsv = string.Join(",", quoteCurrencyCodes.Select(ToProviderCode));
            string url = $"{_settings.Frankfurter.BaseUrl}/rates?base={apiBase}&quotes={quotesCsv}";

            List<FrankfurterRateEntry> entries = await _httpClient.GetFromJsonAsync<List<FrankfurterRateEntry>>(url, cancellationToken)
                ?? throw new HttpRequestException($"Frankfurter returned an empty response for base {baseCurrencyCode}.");

            DateTime fetchedAt = DateTime.UtcNow;

            return entries.Select(entry =>
            {
                bool quoteIsMetal = MetalCodeTranslation.ContainsKey(entry.Quote);

                // Frankfurter's rate is always "1 base *native* unit = rate quote *native*
                // units," where a metal's native unit is the troy ounce, not the gram this
                // system stores everything in. Rescale both sides from troy-oz to gram —
                // the two factors cancel out when base and quote are both metals.
                decimal rate = entry.Rate;
                if (baseIsMetal)
                    rate /= TroyOunceToGrams;
                if (quoteIsMetal)
                    rate *= TroyOunceToGrams;

                return new RateQuote
                {
                    FromCurrencyCode = baseCurrencyCode,
                    ToCurrencyCode = MetalCodeTranslation.TryGetValue(entry.Quote, out string? mappedQuote) ? mappedQuote : entry.Quote,
                    RatePerUnit = rate,
                    RatePerGram = null,
                    FetchedAt = fetchedAt,
                    Source = SourceName,
                };
            }).ToList();
        }

        private static string ToProviderCode(string currencyCode)
            => ProviderCodeTranslation.TryGetValue(currencyCode, out string? mapped) ? mapped : currencyCode;

        public async Task<RateQuote> GetMetalRateAsync(string metalCode, CancellationToken cancellationToken = default)
        {
            string url = $"{_settings.Frankfurter.BaseUrl}/rates?base={metalCode}&quotes=USD";

            List<FrankfurterRateEntry> entries = await _httpClient.GetFromJsonAsync<List<FrankfurterRateEntry>>(url, cancellationToken)
                ?? throw new HttpRequestException($"Frankfurter returned an empty response for metal code {metalCode}.");

            FrankfurterRateEntry? usdEntry = entries.FirstOrDefault(e => string.Equals(e.Quote, "USD", StringComparison.OrdinalIgnoreCase));
            if (usdEntry is null)
                throw new HttpRequestException($"Frankfurter response for {metalCode} did not include a USD rate.");

            string normalizedCode = MetalCodeTranslation.TryGetValue(metalCode, out string? mapped) ? mapped : metalCode;

            return new RateQuote
            {
                FromCurrencyCode = normalizedCode,
                ToCurrencyCode = "USD",
                RatePerUnit = usdEntry.Rate,
                RatePerGram = usdEntry.Rate / TroyOunceToGrams,
                FetchedAt = DateTime.UtcNow,
                Source = SourceName,
            };
        }

        // Currency-sync source for RateService.SyncAsync (sub-task 7) — the code→name→symbol
        // list backing the "currency list grows from Frankfurter" design. Frankfurter-specific,
        // not part of IRateProvider (gold-api.com has no equivalent endpoint).
        public async Task<List<FrankfurterCurrencyEntry>> GetCurrenciesAsync(CancellationToken cancellationToken = default)
        {
            string url = $"{_settings.Frankfurter.BaseUrl}/currencies";

            return await _httpClient.GetFromJsonAsync<List<FrankfurterCurrencyEntry>>(url, cancellationToken)
                ?? throw new HttpRequestException("Frankfurter returned an empty response for the currency list.");
        }

        // Frankfurter v2 /rates returns a JSON ARRAY of one {date, base, quote, rate} object
        // per quote currency — not a single object with a nested rates dictionary.
        private class FrankfurterRateEntry
        {
            [JsonPropertyName("date")]
            public string Date { get; set; }

            [JsonPropertyName("base")]
            public string Base { get; set; }

            [JsonPropertyName("quote")]
            public string Quote { get; set; }

            [JsonPropertyName("rate")]
            public decimal Rate { get; set; }
        }
    }

    // Live-verified shape of GET /v2/currencies: a flat array of {iso_code, iso_numeric, name,
    // symbol, start_date, end_date} — not a code→name dictionary as the plan originally assumed.
    // end_date is today-or-later for currencies Frankfurter still actively rates; historical/
    // decommissioned codes (e.g. pre-Euro currencies) carry a past end_date and are filtered out
    // by the caller rather than seeded as live currencies.
    public class FrankfurterCurrencyEntry
    {
        [JsonPropertyName("iso_code")]
        public string IsoCode { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("symbol")]
        public string Symbol { get; set; }

        [JsonPropertyName("end_date")]
        public string EndDate { get; set; }
    }
}
