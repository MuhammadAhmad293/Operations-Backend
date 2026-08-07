# AI Agent Guidance for Meezan Backend

This is a .NET 10 Clean Architecture / Layered backend system. AI agents should refer to [REPOSITORY_GUIDE.md](REPOSITORY_GUIDE.md) for architectural details, request lifecycle, and project philosophy.

## Quick Reference

**Architecture Pattern:** Clean Architecture / Layered (Dependency Injection inward toward Domain)
**ORM:** Entity Framework Core 10 (SQL Server) with Repository + Unit of Work
**Mapping:** Mapster (convention-based, compile-time mapper)
**Background Jobs:** Hangfire (with dedicated SQL Server database)
**DI Container:** Built-in Microsoft DI (configuration in Resolver classes, not Program.cs)

## Local Setup (First-Time / After Clone)

**Target Framework:** .NET 10

**Requirements:**

- SQL Server running on `localhost` (any edition: Express, Developer, Standard, etc.)
- A SQL Server login with appropriate credentials

**Connection string files to configure (both must use SQL Server format):**

1. `Meezan/appsettings.json` — `DBConString` and `HFDBConString` (creates two databases: `Operation` and `HangfireOperation`)
2. `EFCoreMigrationExcution/appsettings.json` — `DBConString` (used by the migration runner)

Format: `Server=localhost;Initial Catalog=<db>;User Id=<user>;Password=<pass>;TrustServerCertificate=True`

**Steps to get running:**

1. `dotnet restore Meezan.sln`
2. `dotnet run --project EFCoreMigrationExcution` — creates DB and applies all migrations
3. `dotnet run --project Meezan` — starts the API (Swagger UI at `/`, Hangfire dashboard at `/hangfire`)

## Project Structure & Layer Responsibilities

```
Meezan/               → API Controllers, Middlewares, Swagger, Settings
├─ Controllers/         → Only HTTP orchestration; delegate to IServices
├─ Program.cs          → DI registration via static Resolver classes
└─ ErrorHandlingMiddleware.cs → Global exception mapping

Meezan.Services/     → Business Logic (validation, mapping, orchestration)
├─ UserService.cs       → Example showing standard pattern
├─ Resolver/            → CoreServicesResolver.cs (DI bindings)
└─ Mapper/             → Mapster configuration

Meezan.Repositories/ → Data Access (EF Core, DbContext, Migrations)
├─ AppDbContext.cs      → Entity configuration via IEntityTypeConfiguration
├─ UnitOfWork.cs        → Transaction boundary; always call CommitAsync()
├─ Migrations/          → EF migrations directory
└─ Resolver/            → UnitOfWorkResolver.cs (DI bindings)

Meezan.DataModel/    → Domain Entities (POCOS; no behavior)
Meezan.Dto/          → Request/Response DTOs (API contracts)
Meezan.IServices/    → Service Abstractions
Meezan.IRepositories/ → Repository & UnitOfWork Abstractions
Common/                  → Cross-cutting utilities (PasswordHash, HttpClient, MailSender)
```

## Critical Conventions

### 1. Data Flow Rule (Must Follow)

```
API Controller → IService (DTOs) → Mapster → Entity → UnitOfWork.CommitAsync() → SaveChanges
```

- **Never** place queries, DbContext access, or business validation in Controllers
- **Always** persist via `UnitOfWork.CommitAsync()` (not implicit EF SaveChanges)
- **Always** map between DTOs (API) and Entities (DB) using Mapster

### 2. Service Implementation Pattern

When working in `Meezan.Services/{Feature}Service.cs`:

1. Receive DTOs from Controller
2. Validate input (throw custom exceptions: `NameRequiredException`, `InvalidRequestException`)
3. Map DTO → Entity using Mapster
4. Execute repository Meezan via `UnitOfWork.{Repository}.{Operation}()`
5. Handle side-effects (e.g., queue emails, trigger jobs)
6. Call `await UnitOfWork.CommitAsync()` before returning
7. Map Entity → DTO for response
8. Return wrapped in `ResponseDto<T>` (standardized API envelope)

### 3. Dependency Injection (Resolver Classes)

- Do NOT add service registrations directly in `Program.cs`
- Create or update Resolver static classes (`*Resolver.cs`) with `Resolve*` methods
- Register interface → implementation pairs via `builder.Services.AddScoped/Transient/Singleton`
- **Example:** `CoreServicesResolver.cs`, `UnitOfWorkResolver.cs`, `CommonResolver.cs`
- Call resolver methods from `Program.cs` only

### 4. Entity Framework & Unit of Work

- All DB interactions must go through `IUnitOfWork` (injected into Services)
- No direct `DbContext` access from Services
- `Lazy<AppDbContext>` in `Lazier.cs` defers initialization until first DB call (startup performance)
- Call `UnitOfWork.CommitAsync()` **once per logical transaction**
- Entities are Anemic POCOs; all behavior lives in Services

### 5. Request/Response Contracts

- Use **generic `ResponseDto<T>`** for all API responses
- Structure: `{ Status: ResponseStatus, Message: string, Data: T }`
- Always return wrapped responses from Controllers
- DTOs should NOT expose domain entity internals

## Extending the System

For new features, follow this sequence (see [REPOSITORY_GUIDE.md - How To Extend The System](REPOSITORY_GUIDE.md#how-to-extend-the-system)):

1. **Domain:** Add Entity in `Meezan.DataModel.Entities`
2. **Schema:** Configure via `IEntityTypeConfiguration` in `Meezan.Repositories`; generate EF migration
3. **DTOs:** Create Request/Response DTOs in `Meezan.Dto.DTOs`
4. **Interfaces:** Add `I{Entity}Repository` and `I{Entity}Service`
5. **Repository:** Implement in `Meezan.Repositories.Repository`; hook to `UnitOfWork`
6. **Service:** Implement in `Meezan.Services` with validation, mapping, and `CommitAsync()` calls
7. **Controller:** Create in `Meezan.Controllers`; only call `IService` methods
8. **DI:** Register service in `CoreServicesResolver.cs` or create new Resolver if needed

## Key Files to Know

| File                                       | Purpose                               | When to Touch                         |
| ------------------------------------------ | ------------------------------------- | ------------------------------------- |
| [REPOSITORY_GUIDE.md](REPOSITORY_GUIDE.md) | Full architecture & patterns          | Understanding design decisions        |
| `Meezan/Program.cs`                        | DI root; calls Resolver classes       | Adding new Resolver registrations     |
| `Meezan.Services/*Service.cs`              | Business logic & validation           | Implementing features                 |
| `Meezan.Repositories/UnitOfWork.cs`        | Transaction scope & repository access | Understanding transaction boundaries  |
| `Meezan.Repositories/AppDbContext.cs`      | EF Core configuration & migrations    | Modifying database schema             |
| `Common/PasswordHash.cs`                   | Password hashing utility              | User authentication features          |
| `Common/Notification/Mail/*`               | Email service integration             | Sending emails or Mail entity changes |

## Common Pitfalls to Avoid

1. ❌ **Placing business logic in Controllers** → Move to Services
2. ❌ **Querying the DbContext directly from Services** → Use IRepository abstractions
3. ❌ **Forgetting to call `UnitOfWork.CommitAsync()`** → Changes won't persist
4. ❌ **Returning domain Entities from API endpoints** → Always map to DTOs
5. ❌ **Adding DI registrations directly in Program.cs** → Use Resolver classes
6. ❌ **Not creating migrations after Entity changes** → Schema won't match model
7. ❌ **Using AutoMapper instead of Mapster** → Project uses Mapster for compilation & performance

## Testing & Development

- **Build:** `dotnet build` (from solution root or individual project)
- **Run:** `dotnet run` from `Meezan/` directory (API entry point)
- **Migrations:** Use `Meezan.Repositories` project for EF Core migration context
- **Hangfire Dashboard:** Accessible at `/hangfire` endpoint after app startup (if configured in appsettings)

## Glossary

| Term              | Definition                                                                                    |
| ----------------- | --------------------------------------------------------------------------------------------- |
| **Unit of Work**  | Scopes database transactions; `CommitAsync()` batches all pending changes into single DB save |
| **ResponseDto**   | Standardized API envelope: `{ Status, Message, Data }`                                        |
| **Mapster**       | Convention-based object-to-object mapper; compiles mappings at build-time                     |
| **Anemic Domain** | Domain entities hold state only (POCOs); behavior lives in Services                           |
| **Lazy<T>**       | Defers DbContext creation until first database access (startup performance)                   |

## For AI Agents: Summary

This is a textbook Clean Architecture implementation with strict layering. Your job:

- Respect the dependency direction (inward toward Domain)
- Keep Controllers thin; put logic in Services
- Always use the Unit of Work for persistence
- Map between DTOs and Entities; never expose raw entities
- Register DI in Resolvers, not Program.cs
- When confused, check [REPOSITORY_GUIDE.md](REPOSITORY_GUIDE.md)
