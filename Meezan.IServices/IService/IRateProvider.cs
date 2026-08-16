namespace Meezan.IServices.IService
{
    // Anti-corruption adapter interface (spec §7). GetMetalRateAsync throws on failure rather
    // than returning null, so CompositeRateProvider's fallback logic (sub-task 5) can rely on
    // ordinary exception handling instead of null-checking.
    public interface IRateProvider
    {
        Task<List<RateQuote>> GetFiatRatesAsync(List<string> quoteCurrencyCodes, CancellationToken cancellationToken = default);
        Task<RateQuote> GetMetalRateAsync(string metalCode, CancellationToken cancellationToken = default);
    }
}
