using Common.Dto;
using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;
using Meezan.Dto.DTOs.Rate;
using Meezan.IRepositories.UnitOfWork;
using Meezan.IServices.IService;
using Meezan.Services.Base;
using Meezan.Services.CustomExceptions;
using Meezan.Services.Localization;
using Meezan.Services.RateProviders;
using MapsterMapper;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Meezan.Services.RateService
{
    // Real Redis/DB cache-aside read path + direct/inverse/cross-through-USD resolution
    // (spec §7, Phase 010). Only USD→X and {GOLD,SILVER}→USD pairs are ever stored (per
    // FrankfurterRateProvider's USD-pivot design) — every other pair is derived at read time.
    public class RateService : BaseService, IRateService
    {
        private const string CacheKeyPrefix = "rate:";
        private const string UsdCode = "USD";

        // Redis TTL is a safety net, not the primary freshness mechanism — SyncAsync
        // overwrites these keys daily. It just bounds how long a stale cached rate can survive
        // a missed sync run before GetStoredRateAsync falls back to the DB's latest snapshot.
        private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(48);

        // Frankfurter has no Arabic currency names; a small static EN-code→AR-name dictionary
        // covers currencies likely relevant to this app's audience. Anything not listed here
        // falls back to the English name — a content follow-up, not a blocker (per the plan).
        private static readonly Dictionary<string, string> ArabicNameOverrides = new(StringComparer.OrdinalIgnoreCase)
        {
            ["AED"] = "درهم إماراتي",
            ["KWD"] = "دينار كويتي",
            ["QAR"] = "ريال قطري",
            ["BHD"] = "دينار بحريني",
            ["OMR"] = "ريال عماني",
            ["JOD"] = "دينار أردني",
            ["EUR"] = "يورو",
            ["GBP"] = "جنيه إسترليني",
            ["TRY"] = "ليرة تركية",
            ["PKR"] = "روبية باكستانية",
            ["INR"] = "روبية هندية",
            ["MYR"] = "رينغيت ماليزي",
            ["IDR"] = "روبية إندونيسية",
            ["CNY"] = "يوان صيني",
            ["JPY"] = "ين ياباني",
            ["CHF"] = "فرنك سويسري",
            ["CAD"] = "دولار كندي",
            ["AUD"] = "دولار أسترالي",
        };

        private IRateProvider RateProvider { get; }
        private FrankfurterRateProvider Frankfurter { get; }
        private IDistributedCache Cache { get; }
        private ILogger<RateService> Logger { get; }

        public RateService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILocalizationService localization,
            IRateProvider rateProvider,
            FrankfurterRateProvider frankfurter,
            IDistributedCache cache,
            ILogger<RateService> logger)
            : base(unitOfWork, mapper, localization)
        {
            RateProvider = rateProvider;
            Frankfurter = frankfurter;
            Cache = cache;
            Logger = logger;
        }

        public async Task SyncAsync(CancellationToken cancellationToken = default)
        {
            List<Currency> currencies = await UnitOfWork.CurrencyRepository.GetAllAsync();
            List<string> fiatQuoteCodes = currencies
                .Where(c => c.Type == CurrencyType.Fiat && !string.Equals(c.Code, UsdCode, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Code)
                .ToList();

            List<RateQuote> quotes = new();
            if (fiatQuoteCodes.Count > 0)
                quotes.AddRange(await RateProvider.GetFiatRatesAsync(fiatQuoteCodes, cancellationToken));

            quotes.Add(await RateProvider.GetMetalRateAsync("XAU", cancellationToken));
            quotes.Add(await RateProvider.GetMetalRateAsync("XAG", cancellationToken));

            quotes.AddRange(await FetchDirectPairsForInUseCurrenciesAsync(currencies, cancellationToken));

            List<FrankfurterCurrencyEntry> remoteCurrencies = await Frankfurter.GetCurrenciesAsync(cancellationToken);
            string today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
            HashSet<string> existingCodes = currencies.Select(c => c.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

            List<FrankfurterCurrencyEntry> newCurrencies = remoteCurrencies
                .Where(e => string.Compare(e.EndDate, today, StringComparison.Ordinal) >= 0 && !existingCodes.Contains(e.IsoCode))
                .ToList();

            await UnitOfWork.ExecuteInTransactionAsync(_ =>
            {
                foreach (FrankfurterCurrencyEntry entry in newCurrencies)
                {
                    string arName = ArabicNameOverrides.TryGetValue(entry.IsoCode, out string? ar) ? ar : entry.Name;
                    UnitOfWork.CurrencyRepository.Create(new Currency
                    {
                        Code = entry.IsoCode,
                        Type = CurrencyType.Fiat,
                        Symbol = entry.Symbol,
                        Decimals = 2,
                        EnName = entry.Name,
                        ArName = arName,
                        EnDescription = entry.Name,
                        ArDescription = arName,
                        CreationTime = DateTime.UtcNow,
                        LastModificationTime = DateTime.UtcNow,
                    });
                }

                foreach (RateQuote quote in quotes)
                {
                    UnitOfWork.RateSnapshotRepository.Create(new RateSnapshot
                    {
                        FromCurrencyCode = quote.FromCurrencyCode,
                        ToCurrencyCode = quote.ToCurrencyCode,
                        Rate = quote.RatePerGram ?? quote.RatePerUnit,
                        FetchedAt = quote.FetchedAt,
                        Source = quote.Source,
                        CreationTime = DateTime.UtcNow,
                        LastModificationTime = DateTime.UtcNow,
                    });
                }

                return Task.CompletedTask;
            }, cancellationToken);

            foreach (RateQuote quote in quotes)
            {
                decimal rate = quote.RatePerGram ?? quote.RatePerUnit;
                await TryCacheSetAsync(CacheKey(quote.FromCurrencyCode, quote.ToCurrencyCode),
                    rate.ToString(CultureInfo.InvariantCulture), cancellationToken);
            }

            Logger.LogInformation(
                "Rate sync completed: {SnapshotCount} snapshots persisted, {NewCurrencyCount} new currencies discovered",
                quotes.Count, newCurrencies.Count);
        }

        // Beyond the USD-anchored set every currency in the lookup table gets above, fetch
        // *direct* pairs for currencies actually in use — an account's base currency or a
        // non-deleted wallet's currency — against every other in-use currency and against
        // GOLD/SILVER, so the common conversions this app actually needs (e.g. a SAR-base
        // account with a GOLD wallet) resolve from a stored direct snapshot instead of always
        // being derived through USD at read time. One batched Frankfurter call per in-use fiat
        // currency (as `base`) covers every other in-use currency plus both metals as `quotes`
        // in a single request — live-verified that Frankfurter accepts an arbitrary fiat base
        // with metals mixed into `quotes` (`base=SAR&quotes=EGP,XAU,XAG,USD` returns all four).
        // A GOLD/SILVER wallet's own direct pairs come for free as a *quote* under every fiat
        // base's call, so metals are never iterated as a base themselves here.
        private async Task<List<RateQuote>> FetchDirectPairsForInUseCurrenciesAsync(List<Currency> currencies, CancellationToken cancellationToken)
        {
            Dictionary<string, CurrencyType> currencyTypesByCode = currencies.ToDictionary(c => c.Code, c => c.Type, StringComparer.OrdinalIgnoreCase);

            List<string> accountBaseCurrencyCodes = await UnitOfWork.AccountRepository.GetDistinctBaseCurrencyCodesAsync();
            List<string> walletCurrencyCodes = await UnitOfWork.WalletRepository.GetDistinctActiveCurrencyCodesAsync();

            List<string> inUseFiatCurrencyCodes = accountBaseCurrencyCodes
                .Concat(walletCurrencyCodes)
                .Where(code => !string.Equals(code, UsdCode, StringComparison.OrdinalIgnoreCase) &&
                               currencyTypesByCode.TryGetValue(code, out CurrencyType type) && type == CurrencyType.Fiat)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            List<RateQuote> directQuotes = new();
            foreach (string baseCode in inUseFiatCurrencyCodes)
            {
                List<string> quoteCodes = inUseFiatCurrencyCodes
                    .Where(code => !string.Equals(code, baseCode, StringComparison.OrdinalIgnoreCase))
                    .Append("GOLD")
                    .Append("SILVER")
                    .ToList();

                try
                {
                    directQuotes.AddRange(await Frankfurter.GetDirectRatesAsync(baseCode, quoteCodes, cancellationToken));
                }
                catch (Exception ex)
                {
                    // One bad/unsupported in-use currency (e.g. a code Frankfurter doesn't
                    // recognize) must not fail the *entire* sync run — that would silently drop
                    // the already-fetched USD-anchored quotes for every other currency too, since
                    // this all runs before SyncAsync's persistence transaction. Skip just this
                    // base and keep going; the direct/inverse/cross-through-USD resolution chain
                    // still covers this currency from whatever USD-anchored data it does have.
                    Logger.LogWarning(ex, "Direct-pair rate fetch failed for in-use currency {BaseCode}; skipping it this run", baseCode);
                }
            }

            return directQuotes;
        }

        // Reuses CacheTtl as the freshness bar — the same "how stale is too stale before we stop
        // trusting it" window the Redis safety-net TTL already encodes.
        public Task<bool> HasRecentSnapshotAsync(string currencyCode, CancellationToken cancellationToken = default)
            => UnitOfWork.RateSnapshotRepository.HasSnapshotSinceAsync(currencyCode, DateTime.UtcNow - CacheTtl, cancellationToken);

        public async Task<decimal> GetLatestAsync(string fromCurrencyCode, string toCurrencyCode, CancellationToken cancellationToken = default)
        {
            if (string.Equals(fromCurrencyCode, toCurrencyCode, StringComparison.OrdinalIgnoreCase))
                return 1m;

            decimal? direct = await ResolveDirectOrInverseAsync(fromCurrencyCode, toCurrencyCode, cancellationToken);
            if (direct.HasValue)
                return direct.Value;

            bool eitherIsUsd = string.Equals(fromCurrencyCode, UsdCode, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(toCurrencyCode, UsdCode, StringComparison.OrdinalIgnoreCase);

            if (!eitherIsUsd)
            {
                decimal? fromToUsd = await ResolveDirectOrInverseAsync(fromCurrencyCode, UsdCode, cancellationToken);
                decimal? usdToTarget = await ResolveDirectOrInverseAsync(UsdCode, toCurrencyCode, cancellationToken);
                if (fromToUsd.HasValue && usdToTarget.HasValue)
                    return fromToUsd.Value * usdToTarget.Value;
            }

            throw new RatesUnavailableException(Localization.RatesUnavailable);
        }

        public async Task<ResponseDto<RatesResponseDto>> GetLatestQuotesAsync(string baseCurrencyCode, List<string> quoteCurrencyCodes, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(baseCurrencyCode) || quoteCurrencyCodes is null || quoteCurrencyCodes.Count == 0)
                throw new InvalidRequestException(Localization.InvalidRequest);

            ResponseDto<RatesResponseDto> response = new ResponseDto<RatesResponseDto>().GetErrorResponse();

            List<RateDto> rates = new(quoteCurrencyCodes.Count);
            foreach (string quoteCurrencyCode in quoteCurrencyCodes)
            {
                (decimal Rate, DateTime FetchedAt) resolved = await GetLatestWithFetchedAtAsync(baseCurrencyCode, quoteCurrencyCode, cancellationToken);
                rates.Add(new RateDto { QuoteCurrencyCode = quoteCurrencyCode, Rate = resolved.Rate, FetchedAt = resolved.FetchedAt });
            }

            return response.GetSuccessResponse(new RatesResponseDto { BaseCurrencyCode = baseCurrencyCode, Rates = rates });
        }

        // BR-19's "as of {fetchedAt}" display requirement needs the snapshot's timestamp, which
        // the cache-aside decimal-only path (GetLatestAsync, used by the hot AccountService/
        // TransactionService callers) doesn't carry — its Redis values are bare rate strings, and
        // reshaping that cache format risks regressing the already-verified hot path. Since
        // RatesController is comparatively low-traffic, this variant reads the DB directly
        // (skipping Redis) and mirrors GetLatestAsync's same-currency/direct-or-inverse/cross-
        // through-USD structure exactly, just carrying a FetchedAt alongside each rate.
        private async Task<(decimal Rate, DateTime FetchedAt)> GetLatestWithFetchedAtAsync(string fromCurrencyCode, string toCurrencyCode, CancellationToken cancellationToken)
        {
            if (string.Equals(fromCurrencyCode, toCurrencyCode, StringComparison.OrdinalIgnoreCase))
                return (1m, DateTime.UtcNow);

            (decimal Rate, DateTime FetchedAt)? direct = await ResolveDirectOrInverseWithFetchedAtAsync(fromCurrencyCode, toCurrencyCode, cancellationToken);
            if (direct.HasValue)
                return direct.Value;

            bool eitherIsUsd = string.Equals(fromCurrencyCode, UsdCode, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(toCurrencyCode, UsdCode, StringComparison.OrdinalIgnoreCase);

            if (!eitherIsUsd)
            {
                (decimal Rate, DateTime FetchedAt)? fromToUsd = await ResolveDirectOrInverseWithFetchedAtAsync(fromCurrencyCode, UsdCode, cancellationToken);
                (decimal Rate, DateTime FetchedAt)? usdToTarget = await ResolveDirectOrInverseWithFetchedAtAsync(UsdCode, toCurrencyCode, cancellationToken);
                if (fromToUsd.HasValue && usdToTarget.HasValue)
                {
                    DateTime olderLeg = fromToUsd.Value.FetchedAt < usdToTarget.Value.FetchedAt ? fromToUsd.Value.FetchedAt : usdToTarget.Value.FetchedAt;
                    return (fromToUsd.Value.Rate * usdToTarget.Value.Rate, olderLeg);
                }
            }

            throw new RatesUnavailableException(Localization.RatesUnavailable);
        }

        private async Task<(decimal Rate, DateTime FetchedAt)?> ResolveDirectOrInverseWithFetchedAtAsync(string from, string to, CancellationToken cancellationToken)
        {
            RateSnapshot? direct = await UnitOfWork.RateSnapshotRepository.GetLatestByPairAsync(from, to, cancellationToken);
            if (direct is not null)
                return (direct.Rate, direct.FetchedAt);

            RateSnapshot? inverse = await UnitOfWork.RateSnapshotRepository.GetLatestByPairAsync(to, from, cancellationToken);
            return inverse is not null ? (1m / inverse.Rate, inverse.FetchedAt) : null;
        }

        // Steps 1-2 of the resolution algorithm: a direct stored snapshot for (from, to), else
        // its inverse from the (to, from) snapshot. Reused both for the top-level pair and for
        // each leg of the cross-through-USD case (step 3), matching the plan's worked examples
        // exactly (e.g. EGP→SAR's "USD→SAR" leg and SAR→GOLD's "1/GOLD→USD" leg are both just
        // this same direct-or-inverse lookup applied to a different pair).
        private async Task<decimal?> ResolveDirectOrInverseAsync(string from, string to, CancellationToken cancellationToken)
        {
            decimal? direct = await GetStoredRateAsync(from, to, cancellationToken);
            if (direct.HasValue)
                return direct;

            decimal? inverse = await GetStoredRateAsync(to, from, cancellationToken);
            return inverse.HasValue ? 1m / inverse.Value : null;
        }

        // Cache-aside for one exact stored (from, to) direction — Redis first, DB fallback,
        // repopulating Redis on a DB hit. Never calls a provider (BR-19: rates are only ever
        // fetched by SyncAsync). Redis I/O is wrapped by TryCacheGetAsync/TryCacheSetAsync so a
        // Redis outage degrades to DB-only reads rather than throwing — this is the actual
        // BR-19 "never blocks" guarantee for the read path (AbortOnConnectFail=false in
        // Program.cs only covers startup; individual commands still fail while disconnected).
        private async Task<decimal?> GetStoredRateAsync(string from, string to, CancellationToken cancellationToken)
        {
            string cacheKey = CacheKey(from, to);
            string? cached = await TryCacheGetAsync(cacheKey, cancellationToken);
            if (cached is not null && decimal.TryParse(cached, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal cachedRate))
                return cachedRate;

            RateSnapshot? snapshot = await UnitOfWork.RateSnapshotRepository.GetLatestByPairAsync(from, to, cancellationToken);
            if (snapshot is null)
                return null;

            await TryCacheSetAsync(cacheKey, snapshot.Rate.ToString(CultureInfo.InvariantCulture), cancellationToken);

            return snapshot.Rate;
        }

        private async Task<string?> TryCacheGetAsync(string key, CancellationToken cancellationToken)
        {
            try
            {
                return await Cache.GetStringAsync(key, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Redis read failed for key {CacheKey}; falling back to the DB", key);
                return null;
            }
        }

        private async Task TryCacheSetAsync(string key, string value, CancellationToken cancellationToken)
        {
            try
            {
                await Cache.SetStringAsync(key, value,
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Redis write failed for key {CacheKey}; continuing without cache", key);
            }
        }

        private static string CacheKey(string from, string to) => $"{CacheKeyPrefix}{from}:{to}";
    }
}
