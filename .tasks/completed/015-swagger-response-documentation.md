# 015 – Swagger Response Documentation

## Status

`completed`

## Goal

Every controller action across the Meezan API publishes its real success response
schema and its real error status codes in `swagger.json`, so the file becomes a
complete, accurate, self-documenting contract for the frontend/mobile teams and
future client generation — documentation-only, zero behavior/route/response
changes.

## Context

Full detail and rationale live in the approved plan at
`C:\Users\lenovo\.claude\plans\small-backend-task-document-compiled-crystal.md`.

Currently every action publishes only `"200": { "description": "OK" }` with no
schema anywhere, including the already-shipped Auth/User endpoints. Scope: all 14
feature + Auth/User controllers (`JobTestController` excluded as scaffolding).

Key decisions from planning (see plan file for full reasoning):
- 200 response type = `ResponseDto<T>` with the traced concrete `T` per action
  (`AttachmentController.Download` is the one exception: `FileStreamResult`).
- Error response body type = `typeof(string)` (matches
  `JsonConvert.SerializeObject(ex.Message)` in `ErrorHandlingMiddleware` exactly)
  — no new DTO class, no middleware changes.
- Error codes per action = traced from actual `throw` sites, not assumed: 400
  (`InvalidRequestException`/`NameRequiredException`), 404
  (`ObjectNotFoundException`), 422 (`UnprocessableEntityException` — only
  `WalletService.Archive` and `CategoryService.Update`/`.Delete` in this
  codebase), 503 (`RatesUnavailableException`, confirmed in-scope with the user
  despite not being in the task brief's example list — it's real and reachable
  from most account/wallet/transaction/zakat actions), 401 (JWT challenge, no
  body — applied class-level on the 13 fully-`[Authorize]` controllers, and
  per-action on `AuthController`'s genuinely-authorized actions only).
- Enum surfacing (`TransactionDto.Type`, `CategoryDto.Kind`, `CurrencyDto.Type`,
  `ZakatCycleDto.Status` — all plain `string` on the wire) done via a new
  `ISchemaFilter` (`EnumStringSchemaFilter`) that injects `Enum.GetNames(...)`
  of the real backing enum into each field's schema — no DTO changes.
- Task brief's own assumption that paying a non-Due Zakat cycle is a 422 is
  wrong — it's actually `InvalidRequestException` (400). Plan uses the real code.

## Sub-Tasks

| # | Description | Status |
|---|-------------|--------|
| 1 | Infrastructure: enable XML doc generation (`Meezan.csproj`, `Meezan.Dto.csproj`), add `EnumStringSchemaFilter`, wire both into `Program.cs`'s `AddSwaggerGen`. Build must stay clean. | ✅ done |
| 2 | `AuthController` + `UserController` response attributes | ✅ done |
| 3 | `AccountController` + `LookupsController` response attributes | ✅ done |
| 4 | `WalletController` + `CategoryController` response attributes | ✅ done |
| 5 | `TransactionController` + `AttachmentController` response attributes | ✅ done |
| 6 | `RatesController` + `OverviewController` + `CalendarController` + `StatisticsController` response attributes | ✅ done |
| 7 | `ZakatController` + `NotificationsController` response attributes | ✅ done |
| 8 | Verification: `dotnet build` clean, run API, fetch/diff `/swagger/v1/swagger.json` before vs after, confirm 200/40x/50x schemas + enum surfacing, write up any newly-found documentation-vs-reality discrepancies | ✅ done |

## Notes

- Sub-task 1 (2026-08-10): `Microsoft.OpenApi` 2.7.5's `ISchemaFilter.Apply` takes `IOpenApiSchema`
  (read-only interface), not the old `OpenApiSchema` concrete type — `EnumStringSchemaFilter` casts
  each matched property schema to `OpenApiSchema` before setting `.Enum` (a `List<JsonNode>` in this
  OpenAPI.NET version, built from `Enum.GetNames(...)`). Found via compiler feedback, not docs —
  Windows PowerShell 5.1 can't reflect into a net10.0 assembly to introspect it up front.
  `dotnet build Meezan.sln` clean (0 errors). Smoke-tested against the live app (real SQL Server,
  not a stub): `/swagger/v1/swagger.json` returns HTTP 200, 45 paths, no exception from the new
  filter (it's a no-op today since no controller yet uses `[ProducesResponseType]`, so none of the
  4 target DTOs are in `components.schemas` yet — the enum injection will start taking effect as
  each controller sub-task adds its `[ProducesResponseType]` attributes). Saved this response as
  the pre-change baseline (`scratchpad/swagger_baseline_before.json`) for sub-task 8's diff. Server
  process stopped cleanly after the check; no `Program.cs` behavior changed beyond the two
  registration lines + `IncludeXmlComments`.

- Sub-task 2 (2026-08-10): `AuthController` — per-action attributes (mixed `[AllowAnonymous]`/
  `[Authorize]`, so 401 is per-action, not class-level): `Register`/`Logout`/`WebLogout` → 200+400;
  `Login`/`WebLogin` → 200+400; `ForgotPassword` → 200 only (service never throws — silent success
  even for an unknown email, by design); `ResetPassword`/`RefreshToken`/`WebRefreshToken` →
  200+400+404; `ChangePassword`/`RevokeSession` → 200+400+401+404; `LogoutAllDevices`/
  `GetActiveSessions` → 200+400+401. No 422/503 anywhere in this controller (confirmed: neither
  `UnprocessableEntityException` nor `RatesUnavailableException` is reachable from `AuthService`).
  `UserController` — class-level 401 (100% `[Authorize]`, no exceptions), then per-action: `Add`/
  `Update` → 200+400 (`ValidateUser` can throw `NameRequiredException`/`InvalidRequestException`,
  both 400); `Delete`/`GetById`/`GetAll` → 200 only.
  - **Found, not fixed (flagging per task step 7, not a doc/reality mismatch — a genuine design
    inconsistency worth the user's attention)**: unlike every other controller in this codebase,
    `UserController`'s service (`UserService`) never throws the typed custom exceptions for
    "not found" cases — `Delete`/`GetById` on a missing id both return HTTP 200 with
    `ResponseDto.Status = Error` and a plain string message, not a 404. This is legacy/scaffold
    behavior (matches `claude-instructions.md`'s own framing of `UserController` as the original
    reference-project scaffold, predating the exception-based pattern every Meezan feature
    controller uses). Documented accurately as-is (200-only, no 404) rather than "corrected" to a
    404 the code doesn't actually produce — changing that would be a behavior change, out of this
    task's scope.
  - `dotnet build Meezan.sln` clean, 0 errors.

- Sub-task 3 (2026-08-10): both controllers are 100% `[Authorize]` with no `[AllowAnonymous]`
  actions, so 401 is class-level on both. `AccountController`: `Get` → 200 (`AccountDto`) + 400 +
  404 + 503 (`RatesUnavailableException` via `ComputeTotalBalanceAsync`'s multi-currency conversion);
  `Create`/`UpdateSettings` → 200 (`EmptyResponseDto`) + 400 + 404, no 503 (neither call path
  converts currency — `Create` seeds a zero-balance wallet, `UpdateSettings` only
  `Enum.TryParse`s `DisplayCalendar`/`Theme`/`Language` with no rate lookups even when
  `BaseCurrencyCode` changes). `LookupsController`: both actions (`GetCurrencies`,
  `GetWalletTypes`) → 200 only, no exceptions thrown anywhere in `LookupService` (plain repository
  reads).
  - `dotnet build Meezan.sln` clean, 0 errors.

- Sub-task 4 (2026-08-10): both controllers put 401 + 400 + 404 at the **class level** — every
  action in both goes through `BaseService.GetAccountByUserIdAsync` first (bad userId claim → 400;
  no Account row → 404), so this is genuinely uniform, not an approximation (confirmed via the
  investigation phase's `BaseService`/`WalletService`/`CategoryService` trace). No `RatesUnavailableException`
  anywhere in either service — neither wallet nor category operations do currency conversion — so no
  503 in this pair, unlike Account/the upcoming Transaction/Zakat controllers.
  `WalletController`: `GetAll`/`Add`/`Update`/`Delete` → 200 only beyond the class-level set;
  `Archive` → adds 422 (`WalletService.Archive`'s non-zero-balance check, the only
  `UnprocessableEntityException` site in `WalletService`). `CategoryController`: `GetTree`/`Add` →
  200 only beyond the class-level set; `Update`/`Delete` → add 422 (both hit `CategoryService`'s
  `IsProtected` check — the category's own 2 of the codebase's total 3 `UnprocessableEntityException`
  sites).
  - `dotnet build Meezan.sln` clean, 0 errors.

- Sub-task 5 (2026-08-10): `TransactionController` — 401+400+404 at class level (uniform: every
  action goes through `GetAccountByUserIdAsync`). `GetFiltered`/`Search` → 200 only beyond that
  (`GetFiltered`'s invalid `type` query string is silently ignored, not thrown — confirmed no extra
  400); `GetById` → 200 only (its own 404 for a missing transaction folds into the class-level 404).
  `Add`/`Update`/`Delete` → add 503: all three run `ZakatEngine.ReevaluateAsync` inside their
  transaction, which can call `RateService.GetLatestAsync` via `ZakatPotCalculator` — including
  plain `Delete`, which has no rate lookup of its own but still inherits this through the shared
  Zakat-reevaluation side effect (a genuinely non-obvious path, confirmed by tracing the call graph
  in the investigation phase, not assumed from the action's own visible logic).
  `AttachmentController` — 401+400+404 at class level (`Upload` throws `InvalidRequestException`
  directly in the controller for a null/empty file, still 400; all three actions resolve the
  account first). `Delete` → 200 only beyond the class-level set. `Upload` → 200
  (`ResponseDto<AttachmentDto>`). `Download` is the plan's one non-`ResponseDto<T>` action — it
  unwraps `ResponseDto<AttachmentContentDto>` itself and returns `File(stream, mimeType, fileName)`,
  documented as `[ProducesResponseType(typeof(FileStreamResult), 200)]` rather than the
  `ResponseDto<T>` pattern, since that's what's actually on the wire (raw bytes with a dynamic
  content-type, not JSON). No 422 anywhere in this pair — `AttachmentService`/`TransactionService`
  never throw `UnprocessableEntityException`.
  - `dotnet build Meezan.sln` clean, 0 errors.

- Sub-task 6 (2026-08-10): all 4 controllers are single- or uniform-action, so every code applies
  at class level. `RatesController.GetLatest` is the **only** action documented so far that skips
  404 — `RateService` never calls `GetAccountByUserIdAsync` (rate lookups aren't account-scoped),
  so its set is 401+400+503 only, confirmed by the investigation phase finding no `ObjectNotFoundException`
  anywhere in `RateService`. `OverviewController`/`CalendarController`/`StatisticsController` all
  get 401+400+404+503 — each goes through `GetAccountByUserIdAsync` (400/404) and
  `BaseCurrencyConverter` for multi-currency totals (503); `CalendarController.GetMonth` additionally
  has its own explicit `month` range check but that's still a 400, same bucket, no new status code.
  `StatisticsController.GetStructure` also validates `kind` via `Enum.TryParse<CategoryKind>`
  (400) and internally calls `OverviewService.GetOverview` (whose own 503 path already applies) —
  both actions share the identical 4-code set, so class-level was accurate for both, not just
  convenient. No 422 in any of the 4 (no `UnprocessableEntityException` site in
  `RateService`/`OverviewService`/`CalendarService`/`StatisticsService`).
  - `dotnet build Meezan.sln` clean, 0 errors.

- Sub-task 7 (2026-08-10): `ZakatController` — all 4 actions share the identical 401+400+404+503
  set (class-level), confirmed uniform via the investigation phase's trace of every
  `ZakatService` method. **Deliberately no 422 anywhere in this controller** — this is the
  correction to the task brief's own assumption, verified again here at implementation time:
  `Pay`/`PayExternal` both check `cycle.Status != ZakatCycleStatus.Due` and throw
  `InvalidRequestException` (already covered by the class-level 400), not
  `UnprocessableEntityException`; grepping `Meezan.Services` confirms `UnprocessableEntityException`
  is thrown from exactly 3 places total in the whole codebase, none in `ZakatService`.
  `NotificationsController.GetLogin` — same 401+400+404+503 set at class level; the 503 is the
  non-obvious one flagged during investigation: `GetLoginNotifications` calls
  `ZakatPotCalculator.ComputePotGoldGramsAsync` (to compute whether the Zakat-reminder notification
  should fire) which internally calls `RateService.GetLatestAsync`, so a rate-unavailable failure
  can surface here even though nothing about "login notifications" suggests a rate dependency.
  - `dotnet build Meezan.sln` clean, 0 errors. **All 14 in-scope controllers now have
    `[ProducesResponseType]` coverage.** Sub-task 8 (verification) is next.

- Sub-task 8 (2026-08-10) — final verification, all checks passed:
  - Cleaned one leftover nullable warning in `EnumStringSchemaFilter.cs` (`propertyKey` is
    genuinely nullable after `FirstOrDefault`). `dotnet build Meezan.sln`: **0 errors** (138
    pre-existing warnings, none new from this task's files after the cleanup).
  - Ran the live app against real SQL Server, fetched `/swagger/v1/swagger.json` (saved as
    `scratchpad/swagger_after.json`, 180KB vs the sub-task-1 baseline's 52KB), and diffed
    programmatically against the pre-change baseline: **identical 45 paths, identical HTTP
    methods, zero non-`responses` field differences** (every path/verb/parameter byte-for-byte
    unchanged — confirmed via a script comparing each operation with `responses` stripped out).
    Only the `responses` objects grew, exactly as intended.
  - **Automated cross-check of every status code count against the design** (summed from all 7
    controller sub-tasks' notes above) matched the live JSON exactly on every code: 200×55 (51
    in-scope + 4 excluded `JobTestController` paths, which correctly kept their bare
    description-only 200 — confirmed untouched), 400×45, 401×42, 404×35, 503×14, 422×3. This
    round-trip (design → code → live JSON, computed independently both directions) is strong
    evidence nothing was missed or double-applied across the 51 in-scope actions.
  - **Enum surfacing confirmed working exactly as designed**: `TransactionDto.type` →
    `enum: [Income, Expense, Transfer]`, `CategoryDto.kind` → `enum: [Income, Expense]`,
    `CurrencyDto.type` → `enum: [Fiat, Metal]`, `ZakatCycleDto.status` →
    `enum: [Active, Due, Broken, Paid]` — all four read straight from the live schema, values
    matching the real `Meezan.DataModel.Enums` member names exactly (via `Enum.GetNames`, so this
    can't drift). `ResponseDto<T>.status` resolves to a `$ref` on a `ResponseStatus` component
    schema (`enum: [0, 1]`) — confirming the native-enum path needed no filter, as expected.
  - Spot-checked 6 structurally distinct endpoints end-to-end against their designed sets:
    `GET /api/Rates/latest` → `[200,400,401,503]` (no 404, confirmed — `RateService` isn't
    account-scoped); `GET /api/Auth/sessions` → `[200,400,401]`; `POST /api/Auth/register` →
    `[200,400]` (no 401, correctly `[AllowAnonymous]`); `GET /api/attachments/{id}` (Download) →
    `[200,400,401,404]` with its 200 body correctly typed `{type: string, format: binary}` (not
    `ResponseDto<T>`, since it's a real file stream); `POST /api/zakat/pay` → `[200,400,401,404,503]`
    with **no 422** (re-confirms the task brief's own wrong assumption doesn't leak into the final
    doc); `DELETE /api/User/{id}` → `[200,401]` only (re-confirms the `UserController` legacy
    no-typed-exceptions finding from sub-task 2).

  - **New finding from this sub-task (documented shape vs. actual runtime response disagree,
    per task step 7 — reported, not silently patched)**: every `401` response across all 13
    `[Authorize]`-gated controllers is documented by Swashbuckle with a `ProblemDetails` schema
    (`content: application/json/text/json/text/plain` all pointing at
    `#/components/schemas/ProblemDetails`), even though this task explicitly declared those 401s
    with **no** type (`[ProducesResponseType(StatusCodes.Status401Unauthorized)]`, `Type =
    typeof(void)` under the hood). This is Swashbuckle's own automatic default for undeclared-type
    4xx/5xx responses on `[ApiController]`-decorated actions, not something this task's code
    requested. **Verified against the real running app**: `curl -i http://localhost:5289/api/wallets`
    with no bearer token returns `401` with `Content-Length: 0` — a genuinely empty body (just a
    `WWW-Authenticate: Bearer` header), no JSON at all, let alone a `ProblemDetails` object. So
    every 401 in the generated contract currently over-promises a body shape the app never sends.
    Not fixed here: correcting it would mean fighting Swashbuckle's built-in default-response
    inference with a new operation/schema filter — a separate, scoped piece of work, not an
    attribute tweak, and this task's instructions are explicit that documentation-vs-reality gaps
    like this should be surfaced for a separate decision rather than papered over. Flagging for the
    user's prioritization.
  - Stopped the test server cleanly; no lingering listener on port 5289.
  - **Task complete**: all 14 in-scope controllers (51 actions) now publish accurate 200 response
    schemas (concrete DTOs, never `object`/`dynamic`) and accurate error status codes traced from
    real `throw` sites, enum-backed string fields surface their allowed values, and the whole
    change is proven behavior/route-neutral by the before/after diff.

## Findings for follow-up (not fixed in this task, flagged per task step 7)

1. **`UserController` never throws the typed exceptions** other controllers use — `Delete`/
   `GetById` on a missing id return HTTP 200 with `Status: Error` in the body, not 404. Legacy
   scaffold behavior predating the exception-based pattern; documented as-is.
2. **Every documented 401 claims a `ProblemDetails` body**, but the real JWT-bearer challenge
   returns an empty body (`Content-Length: 0`) — a Swashbuckle default-inference artifact, not
   something this task's attributes requested. Fixing it needs a schema/operation filter, out of
   this task's attribute-only scope.
3. **The task brief's own example (paying a non-Due Zakat cycle → 422) was incorrect** — the real
   code throws `InvalidRequestException` (400). Documented per the real code, not the brief.

## Approval Log

| Sub-Task | Approved By | Date |
|----------|-------------|------|
| Plan     | User        | 2026-08-10 |
