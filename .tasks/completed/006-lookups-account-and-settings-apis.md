# 006 – Lookups, Account & Settings APIs

## Status

`completed`

## Goal

Implement API contract rows #1–5: first-run account creation, account settings, and the two
seeded-lookup read endpoints.

## Context

Second phase of the full Meezan backend implementation plan approved on 2026-08-07. Depends on
Phase 005 (data layer). Full technical detail lives in the approved plan at
`C:\Users\lenovo\.claude\plans\we-are-starting-a-lively-pudding.md`.

Covers: BR-01, BR-04, BR-12, BR-20; UC-01, UC-02, UC-03; API #1–5. Note: `AccountService.Create`
also creates the default per-account `Category` set (incl. the protected Zakat/Charity category
consumed by Phase 012) and forces the default Cash wallet to a fiat currency — see the plan's
Live Spec Correction section.

## Sub-Tasks

| #   | Description                                                                                          | Status     |
| --- | ------------------------------------------------------------------------------------------------------ | ---------- |
| 1   | `AccountDto`/`AccountSettingsDto`/`CreateAccountDto` + `AccountMapper`                                | ✅ done |
| 2   | `IAccountService`/`AccountService`: `GetByUser`, `Create` (default fiat Cash wallet + default categories incl. protected Zakat/Charity), `UpdateSettings` | ✅ done |
| 3   | `ILookupService`/`LookupService`: `GetCurrencies`, `GetWalletTypes`                                   | ✅ done |
| 4   | `AccountController`, `LookupsController`                                                              | ✅ done |
| 5   | Localization keys: `AccountAlreadyExists`, `AccountNotFound`, `InvalidBaseCurrency`, success message  | ✅ done |
| 6   | DI registration, manual Swagger verification of all 5 routes                                          | ✅ done |

## Notes

- Sub-task 1 (2026-08-08): created `AccountDto` (`Id`, `Name`, `BaseCurrencyCode`, `DisplayCalendar`, `Theme`, `Language` as strings, `TotalBalance`), `AccountSettingsDto` (`BaseCurrencyCode`/`DisplayCalendar`/`Theme`/`Language`, the `PUT /api/account/settings` body), `CreateAccountDto` (`Name`, `BaseCurrencyCode`, nullable `InitialAmount`) in `Meezan.Dto/DTOs/Account/`, and `AccountMapper` (`Account → AccountDto`, relying on Mapster's default enum→string conversion). Enum fields are typed `string` on the DTOs rather than referencing `Meezan.DataModel.Enums` types, since `Meezan.Dto.csproj` has no project reference to `Meezan.DataModel` (by design, matching `UserDto`'s existing simplicity) — introducing one wasn't necessary here. `dotnet build Meezan.sln` succeeds with 0 errors (82 pre-existing warnings, unrelated).
- Sub-task 2 (2026-08-08): the biggest piece so far, several things pulled forward for the build to stay green (same precedent as task 004's sub-tasks 4/5/9):
  - `IRateService`/`RateService` (`Meezan.IServices.IService`/`Meezan.Services.RateService`) — a **placeholder** per the plan's flagged forward-dependency: `GetLatestAsync` returns `1m` for same-currency pairs, throws `RatesUnavailableException` otherwise. Phase 010 replaces this file entirely with the real Redis/DB cache-aside + direct/inverse/cross-through-USD resolution.
  - Two new custom exceptions (`RatesUnavailableException` → 503, `UnprocessableEntityException` → 422 — the latter anticipates BR-05/protected-category needs in later phases) wired into `ErrorHandlingMiddleware`.
  - `IWalletRepository.GetByAccountAsync(accountId)` (pulled forward from Phase 007 sub-task 2) — needed now for `GetByUser`'s total-balance computation.
  - 4 localization keys (`AccountAlreadyExists`, `AccountNotFound`, `InvalidBaseCurrency`, `AccountCreated`) pulled forward from sub-task 5.
  - `IAccountService`/`AccountService`: `GetByUser` (fetches the account + its wallets, computes `TotalBalance` — currently just `Σ InitialAmount × rate` since no `Transaction` write path exists yet; Phase 007's balance helper will extend this once transactions can affect it); `Create` (UC-01 — validates no existing account for the user and that `BaseCurrencyCode` exists, creates the `Account`, forces the default Cash wallet's currency to the base currency if it's `Fiat` else a `USD` fallback constant, clones a static 6-row default-category template — 2 Income, 4 Expense incl. `IsProtected=true` "Zakat/Charity"/"زكاة/صدقة" — into real per-account `Category` rows, naming them in whichever of EN/AR matches the current request's `Accept-Language` culture); `UpdateSettings` (partial-update semantics — each of `BaseCurrencyCode`/`DisplayCalendar`/`Theme`/`Language` only applied if provided/parseable).
  - DI registration for `IAccountService`/`IRateService` in `CoreServicesResolver`.
  - `dotnet build Meezan.sln` succeeds with 0 errors (46 pre-existing warnings, unrelated). Not yet runnable end-to-end (no `AccountController` exists until sub-task 4) — live verification is sub-task 6.
- Sub-task 3 (2026-08-08): created `CurrencyDto`/`WalletTypeDto` (`Meezan.Dto/DTOs/Lookup/`), `LookupMapper` (`Currency → CurrencyDto`, `WalletType → WalletTypeDto`), `ILookupService`/`LookupService` (`GetCurrencies`/`GetWalletTypes`, thin pass-through over `UnitOfWork.CurrencyRepository`/`WalletTypeRepository.GetAllAsync()`), DI registration. `dotnet build Meezan.sln` succeeds with 0 errors (174 warnings, all pre-existing/unrelated).
- Sub-task 4 (2026-08-08): created `AccountController` (`GET/POST /api/account`, `PUT /api/account/settings`) and `LookupsController` (`GET /api/lookups/currencies`, `GET /api/lookups/wallet-types`) — both `[Route("api/[controller]")] [ApiController] [Authorize]`, thin one-liners delegating to the service, `userId` via `User.FindFirstValue(ClaimTypes.NameIdentifier)`, matching `AuthController`/`UserController` exactly. `dotnet build Meezan.sln` succeeds with 0 errors (46 pre-existing warnings, unrelated). **Correction applied in sub-task 6**: plain `[Authorize]` turned out not to work at runtime — see below.
- Sub-task 5 (2026-08-08): verification only, no new code — confirmed all 4 keys (`AccountAlreadyExists`, `AccountNotFound`, `InvalidBaseCurrency`, `AccountCreated`) are wired end-to-end (`ILocalizationService` → `LocalizationService` → `localizationFile.json` en/ar) and actually used via `Localization.*` in `AccountService` (not raw strings), completed as part of sub-task 2's pulled-forward work.
- Sub-task 6 (2026-08-08): ran the API live (`ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile`, local SQL Server + RabbitMQ both reachable — required setting `ASPNETCORE_ENVIRONMENT` explicitly since `--no-launch-profile` skips `launchSettings.json`'s environment, and without it `JwtSettings:Secret` from user-secrets never loads). **Found and fixed a real bug**: `AccountController`/`LookupsController`'s plain `[Authorize]` resolved to ASP.NET Core Identity's cookie challenge instead of JWT Bearer — a request with a valid Bearer token got a 401 with a `Location: /Account/Login` header instead of being authenticated. Root cause not chased into `Program.cs`'s shared auth registration (out of scope, risk of a wide blast radius per Decision D3's spirit); fixed by matching `AuthController`'s already-working `[Authorize(AuthenticationSchemes = "Bearer")]` on both new controllers instead, rebuilt, and re-verified. End-to-end verification via a real registered+logged-in user: `GET /api/lookups/currencies`/`wallet-types` → 200 with seeded data; `GET /api/account` before creation → 404 `AccountNotFound`; `POST /api/account` (SAR, initial 1000) → 200, and confirmed in the DB the default Cash wallet (SAR/1000/WalletTypeId 3) and all 6 default categories including `Zakat/Charity`/`IsProtected=1`; `GET /api/account` after creation → 200 with `totalBalance: 1000.000`; `PUT /api/account/settings` → 200, persisted; duplicate `POST /api/account` → 400 `AccountAlreadyExists`; no-token request → 401; all 5 routes confirmed present in `/swagger/v1/swagger.json`. Stopped the app cleanly afterward.
- **Phase 006 complete.** All 5 API contract rows (#1–5) implemented and live-verified.

## Approval Log

| Sub-Task | Approved By | Date       |
| -------- | ----------- | ---------- |
| Plan     | User        | 2026-08-07 |
