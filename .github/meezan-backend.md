# Meezan — Backend Implementation Guide (.NET Core)

> **Read `meezan-spec.md` first.** It is the canonical source of truth: business rules
> (BR-01…20), data model + ERD, API contract (§6), rate integration architecture (§7),
> and use cases (UC-01…24). This guide only adds backend-specific implementation
> direction for the .NET Core repository. If anything here conflicts with the spec,
> the spec wins.

## 1. Ground rules

- **Reuse the existing user table.** Authentication (login/logout) already exists in this
  backend. Reference the user table via FK from `Accounts.UserId` (unique index — one
  account per user, BR-01). Never scaffold or migrate a new user table.
- Follow this repository's existing architecture and conventions (Clean Architecture
  layering, existing patterns for controllers/services/repositories, DI, validation, and
  error handling). New Meezan features are additions to the existing solution, not a new
  solution.
- Implement the API contract exactly as mapped in spec §6; expose it in Swagger.

## 2. Data layer (EF Core)

- Create entities from spec §5 ERD. Binding decisions (spec §5.2): merged
  self-referencing `Categories` (app-level max-depth-1 guard), single `Transactions`
  table with the fee self-reference configured `ON DELETE CASCADE` (BR-09), append-only
  `RateSnapshots`, seeded lookups `Currencies` and `WalletTypes` with `NameEn`/`NameAr`.
- Enums persisted as strings: TransactionType, CategoryKind, CurrencyType,
  DisplayCalendar, Theme, Language, ZakatCycleStatus.
- Precision: `decimal(18,2)` fiat amounts, `decimal(18,3)` gram amounts,
  `decimal(18,6)` rates. All text columns Unicode (nvarchar) for Arabic content.
- Soft delete (BR-06): `IsDeleted` on Wallets and Categories + global query filters;
  transaction reads must be able to include soft-deleted references (history display).
- Seed data: currencies (USD, SAR, EGP, GOLD, SILVER), wallet types, and the default
  income/expense category sets — all with EN + AR names.

## 3. Domain logic

- **Balance calculation:** wallet balance = initialAmount + Σ signed transactions
  (income +, expense −, transfer − on source / + convertedAmount on destination, fees −).
  Prefer computing from transactions (single source of truth) with caching over a stored
  balance column; if a stored column is used, it must be recomputed transactionally.
- **Karat conversion (BR-03):** `pureGoldGrams = amount * karat / 24m` computed on save
  for gold entries; never trust a client-supplied pure value.
- **Fee handling (BR-09):** parent + fee created in one DB transaction; delete cascades.
- **Hijri dates (BR-08):** store both dates on every transaction. Server-side conversion
  with `System.Globalization.UmAlQuraCalendar` (Umm al-Qura — the Saudi civil calendar).
  Hawl arithmetic (start + 1 Hijri year, due − 14 days) uses the same calendar.
- **Zakat engine (UC-22, BR-13…17):** a domain service triggered after every
  balance-affecting write and by the daily rate refresh. Keep it idempotent — recompute
  pot, then transition cycle state (none→Active, Active→Broken, Active→Due, Due→Paid via
  UC-24). Persist due-day valuations on the cycle row.
- **Attachments:** validate size ≤ 10 MB and content type (PDF + images) server-side;
  store outside the DB (file storage path in `Attachments.StoragePath`).

## 4. Rate integration (spec §7 — implement as specified)

- `IRateProvider` (anti-corruption interface) → `FrankfurterRateProvider` (v2 endpoints,
  troy-ounce → gram normalization ÷ 31.1034768) and `GoldApiRateProvider` (metals
  fallback), composed by `CompositeRateProvider`.
- Typed `HttpClient` via `IHttpClientFactory` + Polly: timeout, exponential-backoff
  retry, circuit breaker.
- `RateSyncJob` scheduled daily (config-driven cron) via the solution's existing
  background-job mechanism (e.g., Hangfire or a hosted service): fetch → insert
  snapshots → refresh Redis.
- **Redis** is the cache layer (not IMemoryCache): key per pair, value = latest snapshot;
  cache-aside reads with DB fallback.
- Config: provider base URLs, cron, Redis connection — all in appsettings; no secrets
  needed (Frankfurter is keyless).

## 5. Localization (BR-20)

- `Accept-Language` request-culture middleware (`en` default, `ar` supported).
- `IStringLocalizer` + `.resx` (en/ar) for validation messages and system strings —
  including "The total has not reached the nisab" and the zakat login toast text.
- Never localize numbers into Eastern Arabic digits — invariant/Western digit formatting
  in API payloads; the client formats for display (also Western digits per BR-20).

## 6. Notifications (BR-16, UC-23)

- On login (or first authenticated call of a session), evaluate the reminder condition
  (Active cycle within 14 days of due, pot ≥ nisab) and expose it via
  `GET /api/notifications/login`. No email/push in this phase.

## 7. Testing priorities

1. Karat conversion table (all five karats, 3-decimal rounding).
2. Zakat state machine: nisab reach → Active; drop → Broken; full Hijri year → Due;
   pay → Paid (+ conditional new Active). Include Hijri boundary dates.
3. Fee cascade on parent delete.
4. Soft-delete dropdown semantics (BR-06) at the API level.
5. Cross-currency transfer math with overridden rate.
6. Rate job: normalization, append-only behavior, fallback path, circuit-breaker open.
