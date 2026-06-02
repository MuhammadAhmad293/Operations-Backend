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
- Hangfire 1.8.3 (background jobs)
- Mapster 7.3.0 (object mapping via IRegister)
- Swashbuckle / Swagger (development only)
- Custom JSON-based localization (en/ar)
- SMTP email via System.Net.Mail

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

### `swagger-filters`
**Purpose:** Augment Swagger operation metadata globally.
**Examples:** `Operations/Filter/SwaggerHeaderFilter.cs`
**Conventions:**
- Implement `IOperationFilter`; null-check `operation.Parameters` before adding.
- Registered globally via `c.OperationFilter<T>()` in `AddSwaggerGen`.

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
**Examples:** `Common/Notification/Mail/MailSender.cs`
**Conventions:**
- Use `IMailSender.SendMail(MailDto, MailSettingDto)` from services — never direct SmtpClient.
- Prepare both DTOs with `PrepareMailDtos` before calling; commit Mail entity in the same `CommitAsync()` call as the business entity.

### `background-jobs` (service side)
**Purpose:** Hangfire job definitions.
**Examples:** `Operations.Services/Job/JobService.cs`
**Conventions:**
- `IJobService` / `JobService` hold job body logic.
- Job methods are `void`, not `Task`.

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

### Notification
- **Atomic persistence** — Mail entity and business entity must be committed in the same `CommitAsync()`.
- **Use IMailSender** — never instantiate `SmtpClient` in application code.

### Background Jobs
- **Hangfire only** — do not use `IHostedService` or `BackgroundService` for scheduled work.
- **Job logic in IJobService** — not inlined in controller lambda bodies.

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
- `IHostedService` / `BackgroundService` — use Hangfire.
- `.resx` files or `IStringLocalizer` — use the JSON localization system.
- Direct `SmtpClient` usage in services — use `IMailSender`.
- Repositories that access `AppDbContext` without going through `Lazy<AppDbContext>`.
- Controllers with try/catch blocks or business logic.
- Data annotations (`[Required]`, `[MaxLength]`) on entities — use Fluent API configurations.
