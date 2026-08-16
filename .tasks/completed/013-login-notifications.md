# 013 – Login Notifications

## Status

`completed`

## Goal

Implement API contract row #28: the Zakat reminder toast shown at login.

## Context

Ninth phase of the full Meezan backend implementation plan approved on 2026-08-07. Depends on
Phase 012 (Zakat Engine). Full technical detail lives in the approved plan at
`C:\Users\lenovo\.claude\plans\we-are-starting-a-lively-pudding.md`.

Covers: BR-16; UC-23; API #28.

## Sub-Tasks

| #   | Description                                                                                    | Status     |
| --- | ------------------------------------------------------------------------------------------------ | ---------- |
| 1   | `INotificationService`/`NotificationService.GetLoginNotifications`                              | ✅ done |
| 2   | `NotificationsController`                                                                       | ✅ done |
| 3   | Localization: the zakat reminder toast text                                                     | ✅ done |
| 4   | DI registration, manual verification at the 14-day boundary                                     | ✅ done |

## Notes

- Sub-task 1 (2026-08-09): added `LoginNotificationDto {Type, Message}` (`Meezan.Dto/DTOs/Notification/`) — `Type` lets the frontend distinguish future notification kinds without a breaking response-shape change, even though only `"ZakatReminder"` exists today (API #28's own description says "toasts", plural, hence a `List<>` return). Added `INotificationService`/`NotificationService.GetLoginNotifications`, plus the `ZakatReminderToast` localization key (added now rather than deferred to sub-task 3, matching this session's established practice) and its DI registration (also added now, ahead of the formal sub-task 4, same rationale).
  - **Logic** (BR-16/UC-23, reusing `IZakatCycleRepository.GetCurrentActiveByAccountAsync` and `IZakatPotCalculator` — no new repository/engine code needed): if the account has no `Active` cycle, no reminder. Otherwise, compute `reminderStart = HawlDueHijri − 14 days` via the existing `IHijriCalendarHelper.AddDays` (already present, no change needed), and include the toast iff `reminderStart ≤ today < HawlDueHijri` (inclusive lower bound — the 14th day itself counts; exclusive upper bound — once `today` reaches `HawlDueHijri`, the next `ReevaluateAsync` trigger flips the cycle to `Due`, at which point this specific "trending toward due" reminder is no longer the relevant message) **and** the pot is still `≥ nisab` ("trending Due" — a pot that's since dropped below nisab is heading for `Broken`, not `Due`, so reminding about an upcoming payment would be misleading).
  - Deliberately **read-only**: never calls `ZakatEngine.ReevaluateAsync` itself — re-evaluating the hawl state machine is not this endpoint's job (already wired to every balance-affecting transaction and the daily rate sync from Phase 012), and login is a very high-frequency call that shouldn't add an extra state-mutation path.
  - `dotnet build Meezan.sln` clean, 0 errors.
  - **Live-verified all six boundary cases** via a temporary `Program.cs` startup hook (no controller yet — sub-task 2), each on its own directly-seeded account (`User`/`Account`/`Wallet`/`ZakatCycle` created straight through `IUnitOfWork`, bypassing the hawl engine entirely to isolate this method's own boundary logic — same rationale as Phase 012 sub-task 4's precedent): `HawlDueHijri` = today+20d → no reminder (outside window); today+14d → reminder shown (inclusive lower bound); today+1d → reminder shown; today+0d (due today) → no reminder (exclusive upper bound); today+5d but wallet funded far below nisab → no reminder (fails the "trending Due" pot check); no `Active` cycle at all → no reminder. All six matched expectations exactly. Deleted all test data and reverted the temporary hook afterward (`git diff Meezan/Program.cs` shows only the pre-existing Phase 010 baseline).

- Sub-task 2 (2026-08-09): added `NotificationsController` (`GET /api/notifications/login`) — thin, `[Authorize(AuthenticationSchemes = "Bearer")]`, matching every other controller's exact shape. `dotnet build Meezan.sln` clean, 0 errors.
  - **Live-verified entirely over real HTTP**, no service-level hooks needed this time: `401` with no auth; `[]` on a fresh account with no active cycle; `[]` immediately after funding above nisab (due date a year out, outside the reminder window). Needed one Hijri offset date to backdate a cycle into the 14-day window precisely — used a tiny, throwaway `Program.cs` hook (no test data, just `IHijriCalendarHelper.AddDays` called directly and printed) to compute `today+5d` exactly rather than guessing a Hijri calendar date by hand, then backdated the real cycle's `HawlDueHijri` to that value via `sqlcmd` — `GET /api/notifications/login` then correctly returned `{"type":"ZakatReminder","message":"Your Zakat payment will be due soon"}`, matching sub-task 1's already-verified service-level logic through the real endpoint end-to-end.
  - Deleted all test data and reverted both temporary `Program.cs` hooks afterward (`git diff` shows only the pre-existing Phase 010 baseline).

- Sub-task 3 (2026-08-09): audit/confirmation pass — `ZakatReminderToast` was already added in sub-task 1 (matching this session's established practice of adding each key exactly when first needed). Scripted the same 3-file parity check used in Phase 012 sub-task 9, now covering **all 48 keys in the app** (47 + this phase's one addition): identical key sets across `ILocalizationService.cs`/`LocalizationService.cs`/`localizationFile.json`, zero duplicate `Key`s, every entry has both `en` and `ar` values, and confirmed `Localization.ZakatReminderToast` has a real C# call site (`NotificationService.cs`, not orphaned). `dotnet build Meezan.sln` clean, 0 errors. No code changes — verification-only.

- Sub-task 4 (2026-08-09): confirmed `INotificationService` is registered exactly once (`CoreServicesResolver.cs`, added in sub-task 1) with no duplicates. Closed the one gap left after sub-tasks 1–2: sub-task 1 verified all six logical cases at the service level, and sub-task 2 verified one interior case (`+5d`) through the real controller — neither had exercised the *exact* boundary edges through the real HTTP endpoint. Ran all three here, on one real registered account funded above nisab, backdating `HawlDueHijri` via `sqlcmd` between calls: `+15d` (one day outside the window) → `[]`; `+14d` (the window's inclusive lower bound) → the `ZakatReminder` toast; `+0d` (due today, the exclusive upper bound) → `[]`. All three matched exactly, closing the loop on end-to-end coverage for this phase. Deleted all test data; no `Program.cs` changes were made this sub-task (real HTTP + `sqlcmd` only), so nothing to revert. `dotnet build Meezan.sln` clean, 0 warnings, 0 errors.

## Approval Log

| Sub-Task | Approved By | Date       |
| -------- | ----------- | ---------- |
| Plan     | User        | 2026-08-07 |
