# 007 – Wallets Domain

## Status

`completed`

## Goal

Implement API contract rows #6–10: wallet CRUD, archive, soft/hard delete, gold entry karat
math.

## Context

Third phase of the full Meezan backend implementation plan approved on 2026-08-07. Depends on
Phase 005/006. Full technical detail lives in the approved plan at
`C:\Users\lenovo\.claude\plans\we-are-starting-a-lively-pudding.md`.

Covers: BR-02, BR-03, BR-04, BR-05, BR-06; UC-04…08; API #6–10.

## Sub-Tasks

| #   | Description                                                                                                       | Status     |
| --- | --------------------------------------------------------------------------------------------------------------- | ---------- |
| 1   | `WalletDto`, `CreateWalletDto`, `UpdateWalletDto` + `WalletMapper`                                               | ✅ done |
| 2   | `IWalletRepository` extra methods                                                                                | ✅ done |
| 3   | `IWalletService`/`WalletService`: `GetAll`, `Add`, `Update`, `Archive` (422 if balance≠0), `Delete` (soft/hard)  | ✅ done |
| 4   | Balance computation helper (shared with Transactions/Overview/Zakat)                                             | ✅ done |
| 5   | `WalletController`                                                                                               | ✅ done |
| 6   | Localization keys: `WalletNotFound`, `WalletCurrencyLocked`, `WalletBalanceNotZero`, `WalletArchived`            | ✅ done |
| 7   | DI registration, manual verification incl. archive-blocked and soft-delete-dropdown behavior                    | ✅ done |

## Notes

- Sub-task 1 (2026-08-08): created `WalletDto` (`Id`, `Name`, `WalletTypeId`, `CurrencyCode`, `Balance`, nullable `Color`/`Icon`, `ExcludeFromTotal`, `IsArchived`), `CreateWalletDto`, `UpdateWalletDto` (`Meezan.Dto/DTOs/Wallet/`), and `WalletMapper` (`Wallet → WalletDto`). **Scope note**: the plan's "karat breakdown for gold" on `WalletDto` is deferred, not omitted — karat lives on `Transaction.Karat`, not `Wallet`, so a real breakdown needs Phase 009's transaction data to exist; shipping a permanently-empty placeholder field now would just be dead weight. `Balance` is populated by the service (currently `InitialAmount` only, same pattern as `AccountDto.TotalBalance` from Phase 006), to be extended once Phase 009's balance helper lands. `dotnet build Meezan.sln` succeeds with 0 errors (95 pre-existing warnings, unrelated).
- Sub-task 2 (2026-08-08): `IWalletRepository.GetByAccountAsync` was already added in Phase 006 sub-task 2 (verified still present, excludes soft-deleted). For "balance-lookup helper used by archive/delete checks": Archive's BR-05 balance check needs no new repository method (`wallet.InitialAmount` is already on the fetched entity); Delete's BR-06/UC-07 soft-vs-hard decision needed a real check for "is this wallet referenced by a transaction," added as `ITransactionRepository.ExistsForWalletAsync(walletId)` (checks both `WalletId` and `ToWalletId`) rather than on `IWalletRepository`, since every existing repository in this codebase only queries its own entity type — pulled forward from Phase 009, same precedent as Phase 006's `GetByAccountAsync` pull-forward. `dotnet build Meezan.sln` succeeds with 0 errors (180 pre-existing warnings, unrelated).
- Sub-task 3 (2026-08-08): pulled forward the 4 localization keys (`WalletNotFound`, `WalletCurrencyLocked`, `WalletBalanceNotZero`, `WalletArchived`) from sub-task 6. Created `IWalletService`/`WalletService`: `GetAll` (account's non-deleted wallets via `GetByAccountAsync`, `Balance = InitialAmount` inline until Phase 009/sub-task 4's shared helper lands), `Add` (UC-04, validates `CurrencyCode`/`WalletTypeId` exist), `Update` (UC-05, blocks currency change via `ExistsForWalletAsync` → `WalletCurrencyLocked`, 400), `Archive` (UC-06, 422 `UnprocessableEntityException` if `InitialAmount ≠ 0`, BR-05), `Delete` (UC-07, soft-delete via `IsDeleted=true` if referenced by a transaction else hard delete, BR-06). Every lookup is scoped to `w.AccountId == account.Id` (never trusts a bare wallet `id` from the caller) to prevent one user reaching another's wallet by guessing an ID. DI registration added. `dotnet build Meezan.sln` succeeds with 0 errors (71 pre-existing warnings, unrelated).
- Sub-task 4 (2026-08-08): added `ITransactionRepository.GetSignedSumForWalletAsync(walletId)` (net signed effect of every transaction touching a wallet: Income adds, Expense/Transfer-as-source subtract by `Amount`, Transfer-as-destination credits `ConvertedAmount ?? Amount`; `IsFee` needs no special case since fee transactions are `Type=Expense`) and a new protected `BaseService.GetWalletBalanceAsync(wallet)` = `InitialAmount + GetSignedSumForWalletAsync` — shared so `WalletService`, `AccountService`, and later Overview/Zakat all compute balance identically. Rewired `WalletService.GetAll`/`Archive` and `AccountService.ComputeTotalBalanceAsync` to use it instead of the previous `InitialAmount`-only placeholders. **Verified live, not just by inspection**: ran the API, inserted a real Income (+500) and Expense (−200) transaction directly via SQL against wallet 1 (balance 1000), and confirmed `GET /api/account` returned `totalBalance: 1300.000` — proving the `Sum(t => t.Type == TransactionType.Income ? t.Amount : -t.Amount)` ternary-in-aggregate actually translates and executes correctly against SQL Server, not just compiles. Cleaned up the test rows afterward. Sub-task 6's localization keys were already completed during sub-task 3's pull-forward — marked done, no new work.
- Sub-task 5 (2026-08-08): created `WalletController` (`GET/POST /api/wallets`, `PUT/DELETE /api/wallets/{id}`, `POST /api/wallets/{id}/archive`) — explicit `[Route("api/wallets")]` since the plural resource name doesn't match the class name; `[Authorize(AuthenticationSchemes = "Bearer")]` applied from the start (Phase 006's fix), thin one-liners, `userId` via `ClaimTypes.NameIdentifier`. `dotnet build Meezan.sln` succeeds with 0 errors (123 pre-existing warnings, unrelated).
- Sub-task 7 (2026-08-08): DI registration confirmed (already done in sub-task 3). Ran the API live and exercised the full matrix with a real user: `GET/POST /api/wallets` (list, create), `POST /api/wallets/{id}/archive` blocked with 422 `WalletBalanceNotZero` on a non-zero-balance wallet and succeeding on a zero-balance one, `PUT /api/wallets/{id}` blocked with 400 `WalletCurrencyLocked` once a transaction referenced the wallet, `DELETE /api/wallets/{id}` **soft**-deleting a referenced wallet (verified via SQL: row still present with `IsDeleted=1`) vs **hard**-deleting an unreferenced one (verified via SQL: row fully gone), `GET /api/wallets` confirming the soft-deleted wallet disappears from the list (BR-06 dropdown-absence), and a second registered user getting 404 `WalletNotFound` trying to archive the first user's wallet (cross-account scoping never leaks existence). Cleaned up all raw-SQL test transaction rows afterward and stopped the app.
- **Phase 007 complete.** All 5 API contract rows (#6–10) implemented and live-verified.

## Approval Log

| Sub-Task | Approved By | Date       |
| -------- | ----------- | ---------- |
| Plan     | User        | 2026-08-07 |
