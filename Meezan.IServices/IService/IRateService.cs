using Common.Dto;
using Meezan.Dto.DTOs.Rate;

namespace Meezan.IServices.IService
{
    public interface IRateService
    {
        // Composite-fetch → append-only RateSnapshot rows → Redis refresh, plus currency-sync
        // from Frankfurter's currency list. Called by the daily scheduled job and once at
        // startup (Phase 010 sub-task 8) — never by a user-facing request path (BR-19).
        Task SyncAsync(CancellationToken cancellationToken = default);

        Task<decimal> GetLatestAsync(string fromCurrencyCode, string toCurrencyCode, CancellationToken cancellationToken = default);

        // Whether this currency already has usable, reasonably fresh rate data — used to decide
        // whether adding a wallet in this currency needs to enqueue a background sync (Phase 016
        // sub-task 3). Never fetches from a provider itself (BR-19).
        Task<bool> HasRecentSnapshotAsync(string currencyCode, CancellationToken cancellationToken = default);

        // API #24 — RatesController's read model. Thin wrapper over GetLatestAsync for each
        // requested quote currency, kept here (rather than in the controller) so it's the
        // controller that stays a one-liner per this codebase's convention.
        Task<ResponseDto<RatesResponseDto>> GetLatestQuotesAsync(string baseCurrencyCode, List<string> quoteCurrencyCodes, CancellationToken cancellationToken = default);
    }
}
