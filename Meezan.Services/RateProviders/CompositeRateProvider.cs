using Meezan.IServices.IService;
using Meezan.Services.Setting;
using Microsoft.Extensions.Logging;

namespace Meezan.Services.RateProviders
{
    // Strategy/fallback composite (spec §7): Frankfurter is the primary source for both fiat
    // and metals; gold-api.com only ever backs up metal quotes (it has no FX endpoint at all,
    // see GoldApiRateProvider.GetFiatRatesAsync). Each downstream gets its own
    // RateResiliencePipeline instance — sharing one pipeline across two unrelated APIs would
    // let a GoldApi outage trip the circuit breaker for Frankfurter calls too (and vice versa).
    public class CompositeRateProvider : IRateProvider
    {
        private readonly FrankfurterRateProvider _frankfurter;
        private readonly GoldApiRateProvider _goldApi;
        private readonly RateResiliencePipeline _frankfurterPipeline;
        private readonly RateResiliencePipeline _goldApiPipeline;
        private readonly ILogger<CompositeRateProvider> _logger;

        public CompositeRateProvider(
            FrankfurterRateProvider frankfurter,
            GoldApiRateProvider goldApi,
            RateIntegrationSettings settings,
            ILoggerFactory loggerFactory)
        {
            _frankfurter = frankfurter;
            _goldApi = goldApi;
            _frankfurterPipeline = new RateResiliencePipeline(settings, loggerFactory.CreateLogger<RateResiliencePipeline>());
            _goldApiPipeline = new RateResiliencePipeline(settings, loggerFactory.CreateLogger<RateResiliencePipeline>());
            _logger = loggerFactory.CreateLogger<CompositeRateProvider>();
        }

        // No fallback for fiat — gold-api.com doesn't offer FX rates, so a Frankfurter failure
        // here propagates as-is (after the pipeline's own retry/circuit-breaker handling).
        public Task<List<RateQuote>> GetFiatRatesAsync(List<string> quoteCurrencyCodes, CancellationToken cancellationToken = default)
            => _frankfurterPipeline.ExecuteAsync(ct => _frankfurter.GetFiatRatesAsync(quoteCurrencyCodes, ct), cancellationToken);

        public async Task<RateQuote> GetMetalRateAsync(string metalCode, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _frankfurterPipeline.ExecuteAsync(ct => _frankfurter.GetMetalRateAsync(metalCode, ct), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Frankfurter metal quote failed for {MetalCode}; falling back to gold-api.com", metalCode);
                return await _goldApiPipeline.ExecuteAsync(ct => _goldApi.GetMetalRateAsync(metalCode, ct), cancellationToken);
            }
        }
    }
}
