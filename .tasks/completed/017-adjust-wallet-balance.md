# 017 – Adjust Wallet Balance

## Status

`completed`

## Goal

Let a user reconcile a wallet's balance to a real-world value by posting a reconciling
Income/Expense transaction for the delta — never by mutating stored history.

## Context

Requested 2026-08-17. Balances drift from reality (cash spent unrecorded, bank rounding, gold
re-weighed); the fix must go through the normal transaction ledger so every downstream
consumer (list, overview, statistics, Zakat) sees it automatically, with no special-cased
"corrected balance" concept anywhere.

## Design decisions

Two findings from reading the existing code change this feature's shape beyond what a literal
reading of the request implies:

1. **The protected-category lookup would break.** `ZakatService.Pay` resolves the account's
   Zakat category with `FirstOrDefaultAsync(c => c.AccountId == account.Id && c.IsProtected)`
   — today safe, since "Zakat/Charity" is the *only* protected category any account has. Adding
   two more protected categories (Balance Adjustment × Income/Expense) makes that query pick an
   arbitrary one of three rows. Fix: add a `Category.SystemPurpose` discriminator
   (`ZakatPayment` | `BalanceAdjustment`, nullable — null for ordinary user categories) and
   scope both lookups (Zakat's existing one, and the new adjustment one) by it. Existing
   Zakat/Charity rows backfill via `UPDATE Category SET SystemPurpose='ZakatPayment' WHERE
   IsProtected=1` in the same migration (safe: that predicate currently matches Zakat/Charity
   and nothing else).
2. **Existing accounts have no way to get the new categories.** Categories are created once, at
   account-creation time (`AccountService.Create`'s `DefaultCategoryTemplate`) — there's no
   seed/migration path that retrofits *existing* accounts when a new default category is added
   later (this is the first time that's needed). Plan: add the two rows to
   `DefaultCategoryTemplate` for brand-new accounts, **and** have the adjust-balance path
   find-or-lazily-create the category for accounts that predate this feature — so it works
   immediately for every account, no backfill migration needed for this part.

## Judgment calls (your instructions asked me to decide and state these)

- **delta = 0 → 422, not a silent no-op.** Matches the existing `WalletBalanceNotZero` precedent
  (`UnprocessableEntityException` for "valid request shape, but nothing to do given current
  state"). A silent 200-with-no-effect risks the user thinking an adjustment was recorded when
  none was.
- **Flag adjustments with a new `Transaction.IsAdjustment` bool, not derived from category.**
  Mirrors the existing `IsFee` column exactly (a system-purpose flag with no relational data of
  its own) rather than the `ZakatCycleId` pattern (a real FK other features also need). Deriving
  "is this an adjustment" from the linked category at read time would mean either an extra
  lookup per transaction or loading every category into memory for every list request — a
  column Mapster maps for free costs nothing and matches how `IsFee` already solves the
  identical problem.
- **No negative-balance guard.** Grepped the whole service layer — nothing currently stops an
  ordinary Expense from taking a wallet negative. Per your instruction to match existing
  behavior rather than invent a rule, the adjustment endpoint applies no floor either.
- **"Hidden from user-facing pickers" needs no new backend filtering.** `CategoryDto` already
  exposes `IsProtected`, and Zakat/Charity is already `IsProtected=true` while still being
  returned by `GET /api/categories` today — hiding protected rows from a *picker* is already
  the frontend's job with data it already has. The new categories inherit this for free.

## Revision (2026-08-17): two modes

User confirmed all prior design decisions (`SystemPurpose` enum incl. renaming to `Zakat`/
`BalanceAdjustment`/implicit-none, lazy category creation, the three judgment calls) and added a
second mode, chosen by the user on the frontend:

- **Mode A — "Adjust by transaction"**: what sub-tasks 1-5 below already covered — a real
  Income/Expense transaction for the delta, `IsAdjustment=true`, editable afterwards.
- **Mode B — "Change initial amount"**: sets `Wallet.InitialAmount` directly (by the same delta,
  added rather than replacing — see math below), no transaction, no history entry. Retroactive:
  every derived balance (including past statistics periods) shifts by the same amount.

### Endpoint shape: two endpoints, not one with a mode field

The two modes return different things (Mode A: "transaction saved"; Mode B: "wallet updated",
no transaction to report) and this codebase's existing convention is one verb-phrase endpoint
per distinct action (`/archive` is its own endpoint, not `PUT /wallets/{id}` with a status
field) — no endpoint anywhere in this API takes a `mode` discriminator. Going with:

- `POST /api/wallets/{id}/adjust-balance` — Mode A. Body: `{ newBalance, note? }`.
- `POST /api/wallets/{id}/set-initial-amount` — Mode B. Body: `{ newBalance }` (no `note` — there's
  nowhere to store one; Mode B creates no row at all).

Both compute the same `delta = newBalance − currentComputedBalance` and both reject `delta == 0`
with 422 (same reasoning as Mode A's original judgment call — consistent between the two rather
than picking a different rule per mode). Mode B applies the delta as
`wallet.InitialAmount += delta` (not "= newBalance" — `InitialAmount` and `currentComputedBalance`
already differ by the transaction history sum, so adding the delta is what actually makes the
*computed* balance equal `newBalance`).

### Your 5 "handle and report on each" points

1. **InitialAmount immutability reversal.** Confirmed `UpdateWalletDto` has no `InitialAmount`
   field today and `WalletService.Update` never touches it — that stays exactly as-is. Only the
   new `set-initial-amount` endpoint will ever write to it going forward.
2. **No caching to worry about.** Grepped for it: `GetWalletBalanceAsync` (`BaseService.cs:26`)
   computes `InitialAmount + signed transaction sum` live on every call, `StatisticsService`'s
   opening/ending balance calls that same method with an `asOfDate` — there is no stored/cached
   balance column anywhere in the schema. A Mode B change is visible everywhere the next time
   anything reads it, with nothing to invalidate.
3. **Zakat re-evaluation — confirmed safe for past cycles, but found a real bug for gold.**
   `ZakatEngine.ReevaluateAsync` (`ZakatEngine.cs:27`) only ever mutates the *current* Active
   cycle (fetched via `GetCurrentActiveByAccountAsync`) — it structurally cannot reach an
   already-Due/Paid cycle's frozen `PotGoldGramsAtDue`/`ZakatDueGoldGrams` (only
   `RecomputeCyclePaymentAsync` touches those cycles, and only their payment-status fields).
   Calling `ReevaluateAsync` after either mode is safe. **However**: `ZakatPotCalculator.
   ComputePotGoldGramsAsync` (`ZakatPotCalculator.cs:47`) sums a GOLD wallet's contribution
   *purely* from `GetSignedPureGoldGramsSumForWalletAsync` (transaction history) — it never adds
   `wallet.InitialAmount` for gold wallets, unlike every other currency (line 54-56, the
   fiat/silver branch, which does `wallet.InitialAmount + signed sum`). This is a **pre-existing
   bug independent of this feature**: a GOLD wallet created with a non-zero opening amount has
   always had that gold silently excluded from the Zakat pot. It surfaced here because Mode B on
   a gold wallet would otherwise change the displayed balance while leaving the Zakat pot
   unchanged — the exact inconsistency point 3 asked me to rule out. **Recommend fixing it as
   part of this task** (one-line change, self-healing at read time, no data migration needed)
   since Mode B is what makes it observable and testable; flagging here rather than silently
   bundling it in case you'd rather track it separately.
4. **Metal wallets in Mode B.** `Wallet.InitialAmount` already has `HasPrecision(18, 3)` — same
   precision as gram amounts elsewhere. No karat involved (karat only ever lives on
   `Transaction`); nothing to change.
5. **Localization** — new keys for both endpoints' success/error messages.

### Mode A edit-path clarification

Re-checked `CategoryService.GetTree` and `TransactionService.ValidateAndResolveAsync`: the
Balance Adjustment category is **not** soft-deleted, so it's returned like any other row by
`GET /api/categories` — "hidden from pickers" is and remains a frontend-only filter (as already
decided). Nothing in the backend needs to change for "selected-but-not-in-dropdown" rendering;
that's the frontend applying the same treatment it already needs for any category filtered from
its own picker view. One clarification worth stating explicitly: `IsAdjustment` is set once at
creation and never cleared by `Update` — mirrors `IsFee`'s existing immutability — so a
transaction stays flagged as an adjustment even after the user recategorizes it away from
Balance Adjustment. I'll add a test confirming re-categorization away from (and, on an
unrelated transaction, explicitly onto) the protected category both succeed without error.

## Sub-Tasks

| #   | Description                                                                                                                                                          | Status  |
| --- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------- |
| 1   | Data model: `Category.SystemPurpose` enum (`Zakat`/`BalanceAdjustment`, nullable) + `Transaction.IsAdjustment` bool, EF migration (incl. Zakat/Charity backfill), fix `ZakatService.Pay`'s now-ambiguous category lookup to key off `SystemPurpose` | ✅ done |
| 2   | Fix the discovered Zakat/gold bug: `ZakatPotCalculator` must include a GOLD wallet's `InitialAmount` in the pot, matching the fiat/silver branch                    | ✅ done |
| 3   | Category seeding: add the two protected Balance Adjustment categories to `DefaultCategoryTemplate`; find-or-lazily-create helper for pre-existing accounts        | ✅ done |
| 4   | Mode A: `ITransactionService.AddAdjustment` (internal, reuses `Add`'s private validation/resolution, sets `IsAdjustment=true`) + `WalletService.AdjustBalance` + `POST /api/wallets/{id}/adjust-balance` | ✅ done |
| 5   | Mode B: `WalletService.SetInitialAmount` + `POST /api/wallets/{id}/set-initial-amount`, shared delta/validation helper with Mode A, localization keys for both modes | ✅ done |
| 6   | Unit tests: delta math (positive/negative/zero/metal-grams) for both modes, post-adjustment recomputed balance == newBalance, wallet validation (not found/archived/deleted), lazy category creation, `IsAdjustment` flag, Mode A edit/recategorize path, Zakat re-evaluation fires for both modes + past Due/Paid cycles unchanged + the gold/`InitialAmount` fix | ✅ done |
| 7   | Update `meezan-spec.md`: API contract rows for both endpoints, ERD (`Category.systemPurpose`, `Transaction.isAdjustment`), BR-06 wording, modeling decision #7, new BR for balance adjustment (both modes + the retroactive-history note) | ✅ done |

## Notes

- Sub-task 1 (2026-08-18): Added `CategorySystemPurpose` enum (`Zakat`/`BalanceAdjustment`, `Meezan.DataModel/Enums/`), `Category.SystemPurpose` (nullable, `HasConversion<string>` mirroring `Kind`), and `Transaction.IsAdjustment` (bool, `HasDefaultValue(false)` mirroring `IsFee`). Generated migration `20260818165138_AddBalanceAdjustmentSupport` and added a hand-written `migrationBuilder.Sql("UPDATE [Category] SET [SystemPurpose] = 'Zakat' WHERE [IsProtected] = 1;")` backfill to the `Up` method (EF's scaffolder only emits the column adds; the backfill was written by hand, same as any other data migration in this codebase). Fixed `ZakatService.Pay`'s category lookup (`c.IsProtected` → `c.SystemPurpose == CategorySystemPurpose.Zakat`) and updated `AccountService`'s `DefaultCategoryTemplate` (extended the tuple with a `SystemPurpose` slot) so the Zakat/Charity row gets tagged correctly for every *new* account too, not just the backfilled existing ones.
  - Applied the migration to the real dev DB (`dotnet ef database update`) rather than just trusting the generated SQL — needed the user to stop their Visual-Studio-hosted IIS Express instance first, since it held the API's build output locked. The migration itself succeeded (verified directly: `IsAdjustment`/`SystemPurpose` columns exist, and all 13 existing "Zakat/Charity" rows across every account now read `SystemPurpose=Zakat`) — a `dotnet ef` timeout on releasing its post-migration advisory lock looked like a failure at first glance but wasn't; confirmed by inspecting `__EFMigrationsHistory` and the actual column/data state directly rather than trusting the CLI's exit output alone.
  - `dotnet test Meezan.Tests` green at 79/79 (unchanged — this sub-task is schema plumbing, no new tests yet; sub-task 6 covers behavior). `dotnet build` clean on `Meezan.Repositories.csproj` and `Meezan.csproj`.

- Sub-task 2 (2026-08-18): `ZakatPotCalculator.ComputePotGoldGramsAsync`'s gold branch now adds `wallet.InitialAmount` alongside the existing `GetSignedPureGoldGramsSumForWalletAsync` transaction sum — one line, matching exactly what the fiat/silver branch already did. Added `ZakatEngineTestFixture.CreateGoldFundedAccountAsync` (a GOLD wallet with a given opening amount, zero transactions) and a new test asserting an 85g-opening gold wallet alone reaches nisab. **Verified the test actually catches the bug**, not just passes coincidentally: temporarily reverted the fix, confirmed the new test fails (`[FAIL]`), then restored the fix and confirmed it passes — the standard I want every regression test in this task to meet. `dotnet test Meezan.Tests` green at 80/80.

- Sub-task 3 (2026-08-18): Extracted `BalanceAdjustmentCategoryName` (`Meezan.Services/CategoryDefaults/`) — one canonical `En`/`Ar` name pair — so `AccountService`'s seed template and `WalletService`'s lazy-create path can never drift apart. Added two rows to `DefaultCategoryTemplate` (Income + Expense, both `IsProtected=true`, `SystemPurpose=BalanceAdjustment`) for new accounts. Added `WalletService.GetOrCreateBalanceAdjustmentCategoryAsync(account, kind, ct)` for existing accounts: looks up by `(AccountId, SystemPurpose=BalanceAdjustment, Kind)` via the generic `FirstOrDefaultAsync` (no new repository method needed — this is a one-off lookup, same as `ZakatService.Pay`'s category lookup), and creates-then-commits immediately if missing, since sub-task 4's caller needs a real persisted `CategoryId` right after to hand to `TransactionService.AddAdjustment`.
  - This helper has no public entry point yet (sub-task 4 is what calls it from `AdjustBalance`), so it isn't unit-tested in isolation here — sub-task 6 already scopes "lazy category creation" as a test target, and testing it through the real `AdjustBalance` flow (rather than in isolation) is the more meaningful test anyway, since that's how it actually gets invoked. Noting this explicitly so the gap is tracked, not silently deferred.
  - `dotnet test Meezan.Tests` green at 80/80 (unchanged — no behavior reachable yet). `dotnet build` clean.

- Sub-task 4 (2026-08-18): Added `ITransactionService.AddAdjustment(userId, CreateTransactionDto, ct)` and its `TransactionService` implementation — calls the exact same private `ValidateAndResolveAsync` that `Add`/`Update` already share, builds the `Transaction` the same way `Add` does (minus fee handling, which adjustments never have), and sets `IsAdjustment=true`. Deliberately takes `userId` (not an already-resolved `Account`) and re-resolves it internally: my first cut passed `Account` directly from `WalletService`, but **`Meezan.IServices` has no project reference to `Meezan.DataModel`** — every existing interface in that project is DTO/Common-only, entities never cross that boundary. Passing an `Account` entity through `ITransactionService` would have been the first violation of that boundary anywhere in the codebase; re-resolving via `userId` (like every other cross-cutting call already does) costs one redundant lookup and keeps the architecture consistent.
  - `WalletService.AdjustBalance`: resolves the wallet (404 if missing/soft-deleted), rejects an archived wallet (422, new `WalletIsArchived` key) and a zero delta (422, new `NoBalanceChange` key — the judgment call from the plan), picks Income/Expense by the delta's sign, resolves the Balance Adjustment category via sub-task 3's lazy-create helper, and calls `AddAdjustment`. Now takes `ITransactionService` as a constructor dependency.
  - Added `POST /api/wallets/{id}/adjust-balance` to `WalletController`, mirroring `Archive`'s exact shape (`dto.Id = id` from the route, 200/422 response types documented).
  - Added `WalletIsArchived`/`NoBalanceChange` through the full 3-file localization wiring (JSON en/ar, `ILocalizationService`, `LocalizationService`) plus `FakeLocalizationService`.
  - `WalletService`'s constructor changed shape (`ITransactionService` added) — updated `TransactionServiceTestFixture`'s instantiation (it already builds a real `TransactionService` instance, just passed it through). `dotnet test Meezan.Tests` and `dotnet build` (both `Meezan.Services.csproj` and `Meezan.csproj`) all green/clean — no new behavior tests yet (still no path exercises `AdjustBalance` end-to-end in the test suite; that's sub-task 6, using the same SQLite-backed `TransactionServiceTestFixture` that's already wired up).

- Sub-task 5 (2026-08-18): Extracted `ResolveAdjustmentAsync(userId, walletId, newBalance)` — resolves account+wallet, rejects missing/deleted (404) and archived (422), computes `delta = newBalance − currentComputedBalance`, rejects zero (422) — now shared by both `AdjustBalance` and the new `SetInitialAmount`. Both modes reject the same two failure conditions identically, so factoring this out means there's exactly one place that logic can drift.
  - `WalletService.SetInitialAmount`: `wallet.InitialAmount += delta` (not `= newBalance` — restated from the plan since it's easy to get backwards: `InitialAmount` and the *computed* balance already differ by the transaction-history sum, so adding the delta is what actually makes the computed balance equal `newBalance`), staged inside `UnitOfWork.ExecuteInTransactionAsync` alongside `ZakatEngine.ReevaluateAsync(account.Id, ct)` — mirrors exactly how `TransactionService.Add` pairs its own write with the same re-evaluation call, so both modes fire the Zakat hook the same way.
  - `WalletService` now also takes `IZakatEngine` (already registered in DI; no new registration needed). Added `POST /api/wallets/{id}/set-initial-amount`, mirroring `adjust-balance`'s controller shape exactly. Added `InitialAmountUpdated` through the full localization wiring (Mode B's distinct success message, since it reuses neither `TransactionSaved` — no transaction is created — nor a generic message).
  - `dotnet test Meezan.Tests` green at 80/80, `dotnet build` clean on `Meezan.Services.csproj`, `Meezan.Tests.csproj`, and `Meezan.csproj`. Still no behavior tests for either mode — sub-task 6 next.

- Sub-task 6 (2026-08-18): Added `WalletServiceAdjustBalanceTests.cs` (17 cases, Mode A) and `WalletServiceSetInitialAmountTests.cs` (11 cases, Mode B), both against the real SQLite-backed `TransactionServiceTestFixture` (real `UnitOfWork`/repositories/`ZakatEngine`/`GoldPurityCalculator`, relational transactions — only `IRateService` is a double). Added `TransactionServiceTestFixture.AddWalletAsync` for GOLD-wallet scenarios.
  - Mode A: positive/negative delta → correct Income/Expense transaction; zero delta → 422, no transaction created; GOLD wallet → `Amount`/`Karat=24`/`PureGoldGrams` all equal the delta exactly; recomputed balance equals `newBalance` for both signs; `IsAdjustment=true` and the linked category's `SystemPurpose=BalanceAdjustment`; the category is created lazily on an account that predates this feature (seeded with only an ordinary category) and reused (not duplicated) on a second same-kind adjustment; 404 for missing/other-account/soft-deleted wallets, 422 for archived; `ReevaluateAsync` fires and can start an Active cycle; editing an adjustment transaction to a different category succeeds and `IsAdjustment` stays `true` afterward (immutable, mirrors `IsFee`); explicitly posting an ordinary transaction against the protected category via the normal `Add` path succeeds without error and does *not* set `IsAdjustment`.
  - Mode B: positive/negative delta updates `InitialAmount` correctly; **the one case worth calling out** — a wallet with existing transaction history (a real -200 Expense already posted) confirms `SetInitialAmount` adds the delta to `InitialAmount` rather than overwriting it with `newBalance` directly (`1000 + 700 = 1700`, not `1500`) — exactly the arithmetic mistake that's easy to make here, now pinned down by a test; zero delta → 422; no transaction ever created; GOLD wallet grams updated directly; 404/422 wallet validation mirroring Mode A; `ReevaluateAsync` fires and can start an Active cycle; **an already-`Due` cycle's frozen `PotGoldGramsAtDue`/`ZakatDueGoldGrams` are provably unchanged** after a large `SetInitialAmount` correction on the same account (seeded a Due cycle directly, applied a correction that would dramatically change the pot, reloaded the Due cycle and asserted its frozen figures are bit-for-bit what was seeded) — this is the point 3 confirmation the plan called for, now backed by a test rather than just code-reading.
  - One test bug caught before it shipped: my first `SetInitialAmount_AppliesTheDeltaOnTopOfExistingTransactionHistory` draft posted an Income transaction against a category seeded as Expense-kind, which correctly failed with `CategoryKindMismatch` from `ValidateAndResolveAsync` — not a product bug, just a wrong test fixture assumption on my part (`CreateAccountWithWalletAndCategoryAsync`'s seeded category is Expense-kind). Fixed by using an Expense transaction and recomputing the expected numbers.
  - `dotnet test Meezan.Tests` green at **107/107** (up from 80). `dotnet build` clean on `Meezan.Tests.csproj` and `Meezan.csproj`.

- Sub-task 7 (2026-08-18): Updated `.github/meezan-spec.md`:
  - **API contract**: rows #30/#31 for the two new endpoints, each including the frontend-copy line you specified verbatim ("Records a dated correction — past reports stay as they were" / "Corrects the opening balance — past reports will change") so that framing survives into whatever implements the frontend, not just this conversation.
  - **ERD**: `Category.systemPurpose` (nullable enum) and `Transaction.isAdjustment` (bool) added; `isProtected`'s description generalized since it now covers two purposes, not one.
  - **BR-06**: generalized from naming "Zakat/Charity" specifically to "protected categories" plural, and added the clarification that a protected category is a *frontend-picker* exclusion (not soft-deleted) — the edit path still renders "selected but not offered" the same way BR-06 already does for a soft-deleted value, for the same underlying reason.
  - **Modeling decision #7**: rewritten for three protected rows instead of one, explaining *why* `systemPurpose` exists (the `IsProtected`-alone lookup ambiguity from sub-task 1) and stating the lazy-creation rationale for pre-existing accounts.
  - **New BR-21**: both modes, the delta formula, the forced category + `isAdjustment` flag for Mode A, `InitialAmount`'s changed immutability story for Mode B, the shared 422-on-zero-delta rule, the Zakat re-evaluation guarantee (fires for both, past Due/Paid cycles never touched), and the explicit "no new negative-balance rule" statement.
  - Also updated **UC-01** (account creation), which previously said "the account's one protected Zakat/Charity category" — now describes all three seeded categories and notes the lazy-creation path for existing accounts, since account creation is where the reader would otherwise expect the full seed list to be documented.
  - No code changes this sub-task — documentation only. `dotnet test Meezan.Tests` still 107/107 (unaffected).

## Approval Log

| Sub-Task | Approved By | Date |
| -------- | ----------- | ---- |
| Plan     | User        | 2026-08-17 |
| 1        | User        | 2026-08-18 |
| 2        | User        | 2026-08-18 |
| 3        | User        | 2026-08-18 |
| 4        | User        | 2026-08-18 |
| 5        | User        | 2026-08-18 |
| 6        | User        | 2026-08-18 |
| 7        | User        | 2026-08-18 |
