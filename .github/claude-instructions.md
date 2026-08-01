# Claude Instructions — Operations Backend

## 🚨 Mandatory Compliance (No Exceptions)

This repository enforces **strict workflow, planning, and coding standards**.
Claude MUST comply with ALL rules defined in the following files BEFORE
understanding, generating, or editing any code:

### Authoritative Governance Files
- `.results/CODE_STANDARDS.md`
- `.results/TASK_PLANNING_AND_WORKFLOW.md`

If any instruction conflicts:
👉 STOP and ask for guidance. Do NOT guess.

---

## Execution Rules

### ✅ Always Plan Before Executing
- No implementation without an approved plan
- Planning must be documented in `.tasks/`

### ✅ Manual Approval Required
- Human approval is required after EVERY sub-task
- Never batch multiple sub-tasks in one execution

### ✅ SOLID Principles
- Enforced as advisory guardrails
- Deviations must be explicitly justified

---

### EF Core Rule
- ALWAYS use `IEntityTypeConfiguration<T>`
- NEVER configure entities inside `OnModelCreating`

---

## Task Tracking (Mandatory)

Claude MUST:
- Read `.tasks/planned/` and `.tasks/active/` at session start
- Resume from first incomplete sub-task
- Follow announce → execute → report → stop cycle

All task logic belongs in `.tasks/` — never inline in chat.

---

## If Standards Are Missing or Outdated
- Stop work
- Propose updates to the standards files
- Do not proceed without approval

---

---

## Code Generation Guide

This document also enables AI tools to generate code that is consistent with the patterns, conventions, and architecture of this codebase. Do not invent practices not observed here.

---

## Project Overview

**Operations Backend** is a .NET 10 ASP.NET Core Web API implementing Clean Architecture. It is a user account management + background job reference project. The domain is simple (user CRUD + email notification), but the infrastructure patterns are the important part to follow.

**Tech Stack:**
- C# / .NET 10, ASP.NET Core Web API
- Entity Framework Core 10 + SQL Server
- Hangfire 1.8.3 (scheduled/on-demand background jobs)
- JWT Bearer authentication (`Microsoft.AspNetCore.Authentication.JwtBearer`) + ASP.NET Core rate limiting
- RabbitMQ.Client 7.x (Outbox/Inbox async email delivery pipeline) + Polly v8 (retry/circuit breaker)
- Mapster 7.3.0 (object mapping via IRegister)
- Swashbuckle / Swagger (development only) with JWT Bearer "Authorize" support
- Custom JSON-based localization (en/ar)
- SMTP email via System.Net.Mail (sent asynchronously through the Outbox pipeline, not inline)

---

## Solution Structure

| Project                       | Role                                                      |
|-------------------------------|-----------------------------------------------------------|
| `Operations`                  | API host: controllers, middleware, filters, Program.cs    |
| `Operations.IServices`        | Service interfaces                                        |
| `Operations.Services`         | Business logic, mappers, localization, custom exceptions  |
| `Operations.IRepositories`    | Repository and UnitOfWork interfaces                      |
| `Operations.Repositories`     | EF Core DbContext, repositories, migrations, UoW impl     |
| `Operations.DataModel`        | Entity models                                             |
| `Operations.Dto`              | Domain DTOs (UserDto, etc.)                               |
| `Operations.Ioc`              | IoC aggregation helper                                    |
| `Common`                      | Cross-cutting utilities: mail, password, file, validator  |
| `EFCoreMigrationExcution`     | Standalone migration runner                               |

---

## File Category Reference

### `api-controllers`
**Purpose:** HTTP entry points. Thin delegators to services.
**Examples:** `Operations/Controllers/UserController.cs`, `Operations/Controllers/JobTestController.cs`
**Conventions:**
- Extend `ControllerBase`, decorated with `[Route("api/[controller]")]` + `[ApiController]`.
- Constructor-inject one `I*Service` into a `private` **property** (not a field).
- Every action returns `Task<IActionResult>` — body is `return Ok(await Service.Method(...))`.
- No try/catch, no business logic, no mapping.

### `middleware`
**Purpose:** Cross-cutting pipeline concerns (currently: global exception handling).
**Examples:** `Operations/ErrorHandlingMiddleware.cs`
**Conventions:**
- Constructor receives `RequestDelegate next` stored as a lowercase field.
- `Invoke(HttpContext context)` wraps `await next(context)` in try/catch.
- Static `HandleExceptionAsync` maps exception types to HTTP status codes via `is` pattern.
- Error body is `JsonConvert.SerializeObject(ex.Message)` — a JSON string, not an object.

### `authentication`
**Purpose:** JWT issuance/validation, `[Authorize]` gating, and password-reset token handling.
**Examples:** `Operations.Services/Auth/IJwtTokenGenerator.cs` / `JwtTokenGenerator.cs`, `Operations.Services/AuthService/AuthService.cs`, `Operations/Controllers/AuthController.cs`, `Operations.DataModel/Entities/PasswordResetToken.cs`
**Conventions:**
- Bearer scheme is the standard `JwtBearerDefaults.AuthenticationScheme` (`"Bearer"`) — no custom scheme name.
- `JwtSettings.Secret` is **never** in `appsettings.json` — bind non-secret fields (`Issuer`, `Audience`, `ExpiryMinutes`, `ResetTokenExpiryMinutes`, `FrontEndBaseUrl`) from config, source the secret from user-secrets (dev) or the `JwtSettings__Secret` env var (prod), and fail fast at startup if it's missing (see `Program.cs`).
- Do not inject `ClaimsPrincipal` into a service. Controllers extract the user id (`User.FindFirstValue(ClaimTypes.NameIdentifier)`) and pass it as a plain `string userId` parameter — e.g. `AuthController.ChangePassword`.
- New endpoints/controllers are `[Authorize]` by default; use `[AllowAnonymous]` + `[EnableRateLimiting("auth")]` (or `"auth-login"`) only for the small set of unauthenticated auth endpoints (register/login/forgot/reset password).
- Password reset tokens: generate `rawToken` as **Base64URL** (`Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+','-').Replace('/','_')`) so it's URL-safe with no escaping; persist only `TokenHash = SHA256(rawToken)` on `PasswordResetToken` — the raw token is never written to the DB, only emailed. On reset, recompute the hash of the received token and look up by `TokenHash`.
- A user has at most one active reset token: before issuing a new one (`ForgotPassword`) and after consuming one (`ResetPassword`), mark all other active tokens for that user `IsUsed = true` via `IPasswordResetTokenRepository.GetActiveByUserIdAsync`.
- Enforce password policy via `IValidatorHelper.ValidatePasswordPolicy(password)` (8+ chars, upper/lower/digit/special) in `Register`, `ChangePassword`, and `ResetPassword`.

### `rate-limiting`
**Purpose:** Per-IP throttling on unauthenticated, abuse-prone endpoints.
**Examples:** `Program.cs` (`AddRateLimiter`), `[EnableRateLimiting("auth")]` on `AuthController` actions.
**Conventions:**
- Fixed-window policies keyed by `httpContext.Connection.RemoteIpAddress`, defined once in `Program.cs` (`"auth"` = 5 req/min, `"auth-login"` = 10 req/min), rejecting with HTTP 429.
- Applied per-action via `[EnableRateLimiting("policyName")]` — never applied class-wide, since most controllers are fully authenticated and don't need it.

### `swagger-filters`
**Purpose:** Augment Swagger operation metadata globally.
**Examples:** `Operations/Filter/SwaggerHeaderFilter.cs`
**Conventions:**
- Implement `IOperationFilter`; null-check `operation.Parameters` before adding.
- Registered globally via `c.OperationFilter<T>()` in `AddSwaggerGen`.
- JWT Bearer support is already wired in `AddSwaggerGen` (`AddSecurityDefinition("Bearer", ...)` + `AddSecurityRequirement`) — don't re-add it. Note: this repo uses **Microsoft.OpenApi 2.x**, whose types live in the `Microsoft.OpenApi` namespace (not `Microsoft.OpenApi.Models`), and references use `OpenApiSecuritySchemeReference("Bearer", document)` rather than the older `OpenApiReference` pattern.

### `entities`
**Purpose:** EF Core entity models.
**Examples:** `Operations.DataModel/Entities/User.cs`, `Operations.DataModel/Entities/Mail.cs`
**Conventions:**
- All entities inherit `BaseEntity` (provides `IsDeleted`, `CreationTime`, `LastModificationTime`).
- Lookup/multilingual entities inherit `BaseMultilingualTextEntity` (adds `EnName`, `ArName`, `EnDescription`, `ArDescription`).
- PK naming: `{EntityName}Id` for lookups (e.g., `MailStatusId`), `Id` for main entities.
- No data annotations — constraints live in `EntityConfiguration` Fluent API.
- No `virtual` on navigation properties (lazy loading is disabled).

### `base-entities`
**Purpose:** Audit trail and multilingual base classes.
**Examples:** `Operations.DataModel/Base/BaseEntity.cs`, `Operations.DataModel/Base/BaseMultilingualTextEntity.cs`

### `dtos`
**Purpose:** Data transfer between API layer and service layer.
**Examples:** `Operations.Dto/DTOs/UserDto.cs`, `Common/Dto/ResponseDto.cs`
**Conventions:**
- Plain POCOs, no validation attributes, no constructors.
- All service methods return `ResponseDto<T>` where T is `EmptyResponseDto` (writes), `TDto` (single read), or `List<TDto>` (list read).
- Initialise in Error state: `new ResponseDto<T>().GetErrorResponse(...)`.
- Use `GetSuccessResponse(...)` / `GetErrorResponse(...)` fluent methods.

### `service-interfaces`
**Purpose:** Contracts for the service layer.
**Examples:** `Operations.IServices/IService/IUserService.cs`
**Conventions:**
- All methods return `Task<ResponseDto<T>>`.
- Write ops → `Task<ResponseDto<EmptyResponseDto>>`, reads → `Task<ResponseDto<TDto>>` or `Task<ResponseDto<List<TDto>>>`.
- Method names: `Add`, `Update`, `Delete`, `GetById`, `GetAll`.

### `services`
**Purpose:** Business logic.
**Examples:** `Operations.Services/UserService/UserService.cs`
**Conventions:**
- Extend `BaseService` (provides `UnitOfWork`, `Mapper`, `Localization`); pass to `base(...)`.
- Group public methods under `#region Public Methods`, private under `#region Private Methods`.
- Validate inputs at the top of public methods — throw typed custom exceptions or return `response.GetErrorResponse(...)`.
- Commit check: `await UnitOfWork.CommitAsync() > default(int)`.
- Hash passwords via injected `IPasswordHash.CreateHash` before persistence.
- Use `Localization.*` for user-facing messages where a key exists.

### `base-services`
**Purpose:** Shared service infrastructure.
**Examples:** `Operations.Services/Base/BaseService.cs`

### `repositories`
**Purpose:** Entity-specific data access.
**Examples:** `Operations.Repositories/Repository/UserRepository.cs`
**Conventions:**
- Extend `BaseRepository<T>` — constructor only passes `Lazy<AppDbContext>` to base.
- Implement the matching `IBaseRepository<T>`-extending interface from `Operations.IRepositories`.

### `base-repositories`
**Purpose:** Generic EF Core CRUD.
**Examples:** `Operations.Repositories/Base/BaseRepository.cs`
**Conventions:**
- Write operations (`CreateAsyn`, `Update`, `Delete`) are synchronous enqueue — no await.
- Reads use `Expression<Func<T, bool>>` predicates.

### `repository-interfaces`
**Purpose:** Repository contracts.
**Examples:** `Operations.IRepositories/IRepository/IUserRepository.cs`
**Conventions:**
- Extend `IBaseRepository<T>`; add entity-specific query methods only when needed.

### `unit-of-work`
**Purpose:** Coordinates repositories and commits.
**Examples:** `Operations.IRepositories/UnitOfWork/IUnitOfWork.cs`, `Operations.Repositories/UnitOfWork/UnitOfWork.cs`
**Conventions:**
- Repository properties instantiate fresh repo objects on each access (shared `Lazy<AppDbContext>`).
- `CommitAsync()` delegates to `AppDbContext.Value.SaveChangesAsync()`.
- Interface lists all repositories as named properties under `#region IRepository`.

### `db-context`
**Purpose:** EF Core database context.
**Examples:** `Operations.Repositories/Context/AppDbContext.cs`
**Conventions:**
- `OnModelCreating` calls `SingularizeTableNames`, `ApplyConfigurationsFromAssembly`, then `SeedInitialData`.
- Table names are singular (class name = table name).
- Add new `DbSet<T>` for each new entity.

### `entity-configurations`
**Purpose:** Fluent API entity setup.
**Examples:** `Operations.Repositories/EntityConfiguration/UserConfiguration.cs`
**Conventions:**
- `internal class` implementing `IEntityTypeConfiguration<T>`.
- Must set `GETDATE()` SQL defaults for `CreationTime` and `LastModificationTime`; `false` default for `IsDeleted`.
- No `ToTable(...)` calls — handled globally.

### `ef-migrations`
**Purpose:** Database schema version control.
**Examples:** `Operations.Repositories/Migrations/`
**Conventions:**
- Generated via EF Core CLI; do not hand-edit migration Up/Down methods.
- The standalone `EFCoreMigrationExcution` project applies migrations outside the API.

### `mappers`
**Purpose:** Mapster object mapping registrations.
**Examples:** `Operations.Services/Mapper/UserMapper.cs`
**Conventions:**
- Implement `IRegister`; call `config.NewConfig<TSource, TDest>()` with any custom mapping rules.
- Discovered automatically via assembly scan in `CoreServicesResolver.ResolveMapper`.

### `localization`
**Purpose:** Bilingual string resolution (en/ar).
**Examples:** `Operations.Services/Localization/LocalizationService.cs`, `localizationFile.json`
**Conventions:**
- New strings: add `{ "Key": "...", "LocalizedValue": { "en": "...", "ar": "..." } }` to `localizationFile.json`.
- Add a property to `ILocalizationService` and implement it in `LocalizationService` using `GetKeyValue("Key", "altValue")`.
- Use `Localization.YourProperty` in services — never `GetKeyValue` directly from a service.

### `custom-exceptions`
**Purpose:** Domain-specific typed exceptions mapped to HTTP codes.
**Examples:** `Operations.Services/CustomExceptions/InvalidRequestException.cs`
**Conventions:**
- One constructor: `public ExceptionName(string message = "Default") : base(message) { }`.
- Location: `Operations.Services/CustomExceptions/`.
- Register the new type in `ErrorHandlingMiddleware` with its HTTP status code.

### `ioc-resolvers`
**Purpose:** DI registration wiring.
**Examples:** `Operations.Services/Resolver/CoreServicesResolver.cs`
**Conventions:**
- `public static class`, methods named `Resolve*`, called from `Program.cs`.
- Never call `BuildServiceProvider` inside a resolver.
- One resolver per project boundary.

### `settings-models`
**Purpose:** Strongly-typed configuration objects.
**Examples:** `Operations.Services/Setting/MailSettings.cs`
**Conventions:**
- Plain POCO, no interface.
- Bind via `builder.Configuration.Bind("Section", instance)` in `Program.cs`.
- Register as Singleton by concrete type: `services.AddSingleton(instance)`.

### `common-utilities`
**Purpose:** Cross-cutting, domain-agnostic helpers.
**Examples:** `Common/PasswordHash/PasswordHash.cs`, `Common/Validator/ValidatorHelper.cs`
**Conventions:**
- Always paired with an interface.
- Registered in `CommonResolver.ResolveCommonServices`.

### `notification`
**Purpose:** Email sending abstraction.
**Examples:** `Common/Notification/Mail/MailSender.cs`, `Operations.Services/Email/ISmtpEmailSender.cs` / `SmtpEmailSender.cs`
**Conventions:**
- Application services (`AuthService`, `UserService`, etc.) **never call `MailSender.SendMail` directly and never send email synchronously**. They create a `Mail` entity (`MailStatusId = Draft`) plus an `OutboxMessage { Mail = mail }` and commit both in the same `CommitAsync()` call as the business entity — see `notification` + `outbox` conventions below.
- Actual SMTP delivery happens only inside `EmailConsumer` (a `background-service`), via `ISmtpEmailSender.SendAsync(mail)`, which wraps `IMailSender` and rethrows on failure so `EmailResiliencePipeline` can classify/retry it.
- `IMailSender.SendMail(MailDto, MailSettingDto)` / `PrepareMailDtos` remain the low-level SMTP wrapper — only `SmtpEmailSender` may call them.

### `background-jobs` (Hangfire — service side)
**Purpose:** Scheduled or on-demand job definitions (Hangfire), for finite units of work triggered by a schedule or an explicit dispatch call.
**Examples:** `Operations.Services/Job/JobService.cs`
**Conventions:**
- `IJobService` / `JobService` hold job body logic.
- Job methods are `void`, not `Task`.
- Dispatched via `IBackgroundJobClient` / `IRecurringJobManager` from a controller action — never call the job method directly.
- Use Hangfire for "run this once/on a schedule" work. For continuous stream processing (queue consumers, connection managers), use `background-service` instead — see below.

### `background-service` (BackgroundService — continuous infrastructure)
**Purpose:** Long-running, always-on processes tied to the app's own lifetime: RabbitMQ consumers, connection managers, and pollers. These are infrastructure, not scheduled jobs, so they use `BackgroundService`/`IHostedService`, not Hangfire.
**Examples:** `Operations.Services/Outbox/OutboxPublisherService.cs`, `Operations.Services/Email/EmailConsumer.cs`, `Operations.Services/Email/DeadLetterHandler.cs`, `Operations.Services/Messaging/RabbitConnectionManager.cs`
**Conventions:**
- Extend `BackgroundService`; inject `IServiceScopeFactory` and open an `AsyncServiceScope` per unit of work (batch/message) to resolve scoped services like `IUnitOfWork` — never resolve scoped services from the constructor.
- Consumers wait for `RabbitConnectionManager.IsConnected` before creating a channel, then `await Task.Delay(Timeout.Infinite, stoppingToken)` after `BasicConsumeAsync` to stay alive.
- Override `StopAsync` to close the channel and `Dispose` to dispose it; always call `base.StopAsync`/`base.Dispose`.
- Pollers (`OutboxPublisherService`) loop `while (!stoppingToken.IsCancellationRequested)`, wrapping each iteration in try/catch so one bad batch doesn't kill the loop, then `await Task.Delay(pollIntervalMs, stoppingToken)`.
- Registered via `builder.Services.AddHostedService<T>()` in `Program.cs`.

### `messaging` (RabbitMQ)
**Purpose:** RabbitMQ connection lifecycle, publishing, and topology declaration for the async email pipeline.
**Examples:** `Operations.Services/Messaging/RabbitConnectionManager.cs`, `IRabbitPublisher.cs` / `RabbitPublisher.cs`, `RabbitTopologyDeclarator.cs`
**Conventions:**
- `RabbitConnectionManager` is the single `IHostedService` owning the `IConnection`; it connects with backoff retry in the background and never blocks host startup. Other components get channels via `GetConnection().CreateChannelAsync(...)`, never open their own `IConnection`.
- `RabbitPublisher` uses Publisher Confirms (`WaitForConfirmsOrDieAsync`) — a publish is only considered successful once confirmed.
- Topology (exchange + `email.send`/`email.retry`/`email.deadletter` queues and dead-letter bindings) is declared once via `RabbitTopologyDeclarator.DeclareAsync`, driven entirely by `RabbitMqSettings` config — no hardcoded queue/exchange names in consumers.
- Retry is DLX-based, not application-level: a NACKed transient failure routes to `email.retry` (TTL) which dead-letters back to `email.send` — do not hand-roll retry loops in a consumer.

### `outbox` (Transactional Outbox / Inbox pattern)
**Purpose:** Guarantees no committed email is lost and no message is processed twice.
**Examples:** `Operations.DataModel/Entities/OutboxMessage.cs`, `ProcessedMessage.cs`, `Operations.Services/Outbox/OutboxPublisherService.cs`
**Conventions:**
- Outbox (publish-side): create `OutboxMessage { Mail = mail }` in the same `CommitAsync()` as the `Mail`/business entity — never publish to RabbitMQ directly from a request-handling service.
- `OutboxStatus` has no `Failed` state: `Pending → Publishing → Published`; a publish failure resets the row back to `Pending` (with `RetryCount`/`LastError`) for the next poll, it never gets stuck or silently dropped.
- Inbox (consume-side): before processing, check `ProcessedMessageRepository.ExistsAsync(messageId)`; after success, `Create(new ProcessedMessage { MessageId, ProcessedAt })` in the same commit as the success state update. This is the source of truth for idempotency — do not dedupe off `Mail.MailStatusId` alone.
- `Mail` carries two independent status dimensions: `MailStatusId` (business: Draft/Sent/Failed) and `DeliveryStatus` (infra: Pending/Queued/Processing/Retrying/DeadLetter) — do not conflate them.
- Message envelope shape is fixed: `{ MessageId: <guid>, MailId: <int>, OccurredAt: <UTC> }`.

### `resilience`
**Purpose:** Wraps flaky external calls (SMTP) with retry + circuit breaking so a struggling dependency doesn't get hammered or take down the consumer.
**Examples:** `Operations.Services/Email/EmailResiliencePipeline.cs`
**Conventions:**
- Built with Polly v8 (`ResiliencePipelineBuilder`) — `AddRetry` then `AddCircuitBreaker`, both driven by `EmailDeliverySettings.Resilience`, no hardcoded thresholds.
- Classify failures as transient vs permanent explicitly (`IsTransientFailure`/`IsPermanentFailure` pattern-matching on exception type/SMTP status code) — never retry a permanent failure (e.g. 5xx SMTP, auth failure).
- Callers check `IsCircuitOpen()` before attempting the call so they can reroute to the retry queue instead of throwing; also catch `BrokenCircuitException` as a fallback.

### `health-checks`
**Purpose:** `/health` readiness probes for the messaging/outbox infrastructure.
**Examples:** `Operations.Services/HealthChecks/RabbitMqHealthCheck.cs`, `OutboxBacklogHealthCheck.cs`
**Conventions:**
- Implement `IHealthCheck`; constructor-inject only what's needed to check (e.g. `RabbitConnectionManager`, `IUnitOfWork`).
- Registered via `AddHealthChecks().AddCheck<T>("name", tags: ["ready"])` in `Program.cs`; exposed via `app.MapHealthChecks("/health")`.
- Prefer graded results (`Healthy`/`Degraded`/`Unhealthy`) with a numeric threshold over a plain up/down check when a backlog or queue depth is involved.

---

## Feature Scaffold Guide

### Adding a New CRUD Domain Entity (e.g., `Product`)

1. **Entity** — create `Operations.DataModel/Entities/Product.cs` extending `BaseEntity`.
2. **Entity Configuration** — create `Operations.Repositories/EntityConfiguration/ProductConfiguration.cs` (internal, `IEntityTypeConfiguration<Product>`) setting GETDATE defaults for audit columns.
3. **DbSet** — add `public DbSet<Product> Product { get; set; }` to `AppDbContext`.
4. **Repository Interface** — create `Operations.IRepositories/IRepository/IProductRepository.cs` extending `IBaseRepository<Product>`.
5. **Repository** — create `Operations.Repositories/Repository/ProductRepository.cs` extending `BaseRepository<Product>`.
6. **UnitOfWork** — add `IProductRepository ProductRepository { get; }` to `IUnitOfWork` and `public IProductRepository ProductRepository => new ProductRepository(AppDbContext);` to `UnitOfWork`.
7. **DTO** — create `Operations.Dto/DTOs/ProductDto.cs` (plain POCO).
8. **Mapper** — create `Operations.Services/Mapper/ProductMapper.cs` implementing `IRegister`.
9. **Service Interface** — create `Operations.IServices/IService/IProductService.cs` returning `Task<ResponseDto<T>>`.
10. **Service** — create `Operations.Services/ProductService/ProductService.cs` extending `BaseService`, implementing `IProductService`.
11. **Register Service** — add `services.AddScoped<IProductService, ProductService>()` in `CoreServicesResolver.ResolveCoreServices`.
12. **Controller** — create `Operations/Controllers/ProductController.cs` extending `ControllerBase`.
13. **Migration** — run `dotnet ef migrations add AddProductEntity` in `Operations.Repositories`.

### Adding a New Localizable String

1. Add entry to `Operations.Services/Localization/LocalizationFileReader/localizationFile.json` (both `en` and `ar`).
2. Add property to `ILocalizationService`.
3. Implement in `LocalizationService` using `GetKeyValue("Key", "altValue")`.

### Adding a New Background Job

1. Add method signature to `Operations.IServices/IJob/IJobService.cs`.
2. Implement in `Operations.Services/Job/JobService.cs`.
3. Add controller action dispatching it via `IBackgroundJobClient` or `IRecurringJobManager`.

### Adding a New Custom Exception

1. Create `Operations.Services/CustomExceptions/ProductNotFoundException.cs` with single-arg constructor.
2. Add `else if (ex is ProductNotFoundException) code = HttpStatusCode.NotFound;` in `ErrorHandlingMiddleware`.

---

## Integration Rules (Architectural Constraints)

### Data Layer
- **SQL Server only** — do not use other EF providers.
- **No lazy loading** — load navigation properties explicitly.
- **Singular table names** — enforced globally; do not override.
- **No direct DbContext in services** — all access through `IUnitOfWork`.

### Service Layer
- **All public methods return `ResponseDto<T>`** — never return raw types or throw to the controller.
- **Commit after staging** — stage all repository operations, then call `CommitAsync()` once.
- **Validate first** — run guard clauses at the top of each public method.
- **Localization for messages** — use `Localization.*` properties for user-facing strings.

### API Layer
- **No business logic in controllers** — delegate everything to the service.
- **No try/catch in controllers** — the middleware handles it.
- **SwaggerHeaderFilter adds Accept-Language automatically** — do not add it to individual operations.
- **`[Authorize]` by default** — new controllers/actions require auth unless there's a specific reason (e.g. the 4 public auth endpoints) to mark `[AllowAnonymous]`.

### Authentication
- **No custom JWT scheme** — standard `JwtBearerDefaults.AuthenticationScheme` (`"Bearer"`).
- **Secret never in appsettings.json** — `JwtSettings.Secret` comes from user-secrets (dev) or `JwtSettings__Secret` env var (prod) only; the app throws at startup if it's missing.
- **No `ClaimsPrincipal` in services** — controllers extract `userId` from claims and pass it as a plain parameter.
- **Reset tokens are hashed at rest** — only `SHA256(rawToken)` is ever persisted (`PasswordResetToken.TokenHash`); the raw token exists only in the emailed link. Generate it as Base64URL, not plain Base64, so it never needs URL-escaping.
- **One active reset token per user** — issuing or consuming a token revokes (`IsUsed = true`) all other active tokens for that user in the same commit.

### Notification / Email Delivery
- **Never send email inline** — application services create `Mail` (Draft) + `OutboxMessage` and commit both atomically; they never call `MailSender`/`ISmtpEmailSender` directly.
- **Outbox → RabbitMQ → Consumer** — `OutboxPublisherService` polls and publishes to RabbitMQ (with Publisher Confirms); `EmailConsumer` does the actual SMTP send via `ISmtpEmailSender`, guarded by `EmailResiliencePipeline` (retry + circuit breaker) and inbox-deduplicated via `ProcessedMessage`.
- **Retry is DLX-based** — a NACKed transient failure routes through `email.retry` (TTL) back to `email.send`; don't hand-roll retry/backoff loops in a consumer.
- **Use IMailSender** (only from `SmtpEmailSender`) — never instantiate `SmtpClient` directly anywhere.

### Background Jobs
- **Hangfire = scheduled/on-demand work** (`IJobService`, dispatched via `IBackgroundJobClient`/`IRecurringJobManager`).
- **BackgroundService/IHostedService = continuous infrastructure** — RabbitMQ consumers (`EmailConsumer`, `DeadLetterHandler`), pollers (`OutboxPublisherService`), and connection managers (`RabbitConnectionManager`) are intentionally `BackgroundService`/`IHostedService`, not Hangfire — they run for the app's whole lifetime rather than as discrete scheduled units. Don't migrate these to Hangfire, and don't use Hangfire for new continuous stream-processing work either.
- **Job logic in IJobService** — not inlined in controller lambda bodies.

### Rate Limiting
- **Per-IP fixed-window, config-defined policies** (`"auth"`, `"auth-login"`) in `Program.cs`, applied via `[EnableRateLimiting("policy")]` on individual actions — only on unauthenticated, abuse-prone endpoints.

### IoC
- **Constructor injection only** — no `[FromServices]`, no service locator.
- **Resolver per project** — each class library registers its own services.

### Localization
- **JSON file only** — do not use `.resx` or `IStringLocalizer`.
- **Both languages required** — every key needs `en` and `ar` entries.

---

## Example Prompt Usage

**Feature request:** "Add a Product entity with CRUD operations."

**Files to generate:**

| File | Category |
|------|----------|
| `Operations.DataModel/Entities/Product.cs` | entities |
| `Operations.Repositories/EntityConfiguration/ProductConfiguration.cs` | entity-configurations |
| `Operations.IRepositories/IRepository/IProductRepository.cs` | repository-interfaces |
| `Operations.Repositories/Repository/ProductRepository.cs` | repositories |
| `Operations.Dto/DTOs/ProductDto.cs` | dtos |
| `Operations.Services/Mapper/ProductMapper.cs` | mappers |
| `Operations.IServices/IService/IProductService.cs` | service-interfaces |
| `Operations.Services/ProductService/ProductService.cs` | services |
| `Operations/Controllers/ProductController.cs` | api-controllers |

**Files to modify:**

| File | Change |
|------|--------|
| `Operations.Repositories/Context/AppDbContext.cs` | Add `DbSet<Product>` |
| `Operations.IRepositories/UnitOfWork/IUnitOfWork.cs` | Add `IProductRepository ProductRepository { get; }` |
| `Operations.Repositories/UnitOfWork/UnitOfWork.cs` | Add repository property |
| `Operations.Services/Resolver/CoreServicesResolver.cs` | Register `IProductService` |
| `Operations.Services/Localization/LocalizationFileReader/localizationFile.json` | Add any new message keys |
| `Operations.Services/Localization/ILocalizationService.cs` | Expose new keys |
| `Operations.Services/Localization/LocalizationService.cs` | Implement new keys |

Then run `dotnet ef migrations add AddProductEntity` in `Operations.Repositories`.

---

## What NOT to Generate

- MVC views, Razor Pages, or any HTML rendering.
- AutoMapper registrations — use Mapster `IRegister`.
- `IHostedService` / `BackgroundService` for **scheduled or on-demand** work — use Hangfire for that. (`BackgroundService`/`IHostedService` IS the correct tool for continuous infra — RabbitMQ consumers, pollers, connection managers — see `background-service` category; don't flag or "fix" those.)
- `.resx` files or `IStringLocalizer` — use the JSON localization system.
- Direct `SmtpClient` usage anywhere — use `IMailSender` (and only call it from `SmtpEmailSender`).
- Synchronous/inline email sending from application services — create `Mail` + `OutboxMessage` and let the Outbox/RabbitMQ pipeline deliver it.
- Raw password-reset tokens persisted to the database — store only `SHA256(rawToken)`; the raw token exists solely in the emailed link.
- `ClaimsPrincipal` injected into a service constructor — extract the user id in the controller and pass it as a parameter.
- Repositories that access `AppDbContext` without going through `Lazy<AppDbContext>`.
- Controllers with try/catch blocks or business logic.
- Data annotations (`[Required]`, `[MaxLength]`) on entities — use Fluent API configurations.
