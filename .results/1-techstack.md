# Tech Stack Analysis

## Core Technology Analysis

### Programming Language

- **C# (.NET 10)** — the entire codebase is C#, targeting `net10.0` with nullable reference types enabled and implicit usings.

### Primary Framework

- **ASP.NET Core 10 Web API** — RESTful HTTP API host (`Microsoft.NET.Sdk.Web`), using minimal hosting model (`WebApplication.CreateBuilder`).

### Secondary / Tertiary Frameworks

- **Entity Framework Core 10** (`Microsoft.EntityFrameworkCore` 10.0.5) with SQL Server provider — code-first ORM, Fluent API entity configuration, EF migrations.
- **Hangfire 1.8.3** — background job scheduling (fire-and-forget, delayed, recurring, continuation jobs), backed by a dedicated SQL Server database.
- **Mapster 7.3.0** — object-to-object mapping with a scanned `IRegister`-based configuration pattern; used instead of AutoMapper.
- **Swashbuckle / Swagger** (`Swashbuckle.AspNetCore` 6.2.3, `SwaggerUI` 10.1.5) — API documentation exposed in Development only with a custom `SwaggerHeaderFilter`.
- **Newtonsoft.Json** — used in the error-handling middleware for serialising exception messages.

### State Management Approach

- No client-side state management (pure API). Server-side state is managed through the **Unit of Work** pattern backed by a scoped `AppDbContext`. Repositories are instantiated lazily via `Lazy<AppDbContext>` to avoid premature DbContext materialisation.

### Other Relevant Technologies / Patterns

- **SMTP email** via `System.Net.Mail` wrapped in a custom `IMailSender` / `MailSender` abstraction (Gmail SMTP by default).
- **PBKDF2 password hashing** via `RNGCryptoServiceProvider` + `Rfc2898DeriveBytes` — implemented in both `Common.PasswordHash` (service) and inline inside `UserService`.
- **Custom localization** — a JSON file-based `LocalizationFileReader` supports English and Arabic (`en`/`ar`), wired through `ILocalizationService`.
- **Custom middleware** `ErrorHandlingMiddleware` maps custom exception types to HTTP status codes (400, 404, 500).
- **CORS** configured for `http://localhost:4200` (Angular front-end assumed).

---

## Domain Specificity Analysis

### Problem Domain

This is a **back-end CRUD reference/practice project** focused on **user account management**. It serves as a learning sandbox for Clean Architecture, EF Core patterns, background jobs, email notifications, and object mapping in .NET.

### Core Business Concepts

- **User lifecycle management** — create, read, update, soft-delete (via `IsDeleted` on `BaseEntity`) user accounts.
- **Credential security** — passwords are hashed with PBKDF2 before persistence.
- **Transactional email** — a welcome email is sent and its metadata is persisted atomically with the user record via `IUnitOfWork.CommitAsync()`.
- **Background jobs** — fire-and-forget, delayed, recurring, and continuation jobs via Hangfire (currently demonstrated but not wired to business logic).

### User Interaction Types

- Standard REST Meezan (POST/PUT/DELETE/GET) via JSON payloads.
- No authentication on the API currently (though `ClaimsPrincipal` injection scaffolding is present in `Program.cs`).

### Primary Data Types and Structures

- **Entities**: `User`, `Mail`, `MailAttachment`, `MailStatus`, `MailType` (all inheriting `BaseEntity`).
- **DTOs**: `UserDto`, `ResponseDto<T>`, `PaginationRequestDto`, `PaginationResponseDto`, `TextValueResponseDto`, `EmptyResponseDto`.
- **Enums**: `ResponseStatus`, `MailStatusEnum`, `MailTypeEnum`, `SortDirection`.

---

## Application Boundaries

### Features Clearly Within Scope

- User CRUD (create with hashed password + welcome email, update, soft-delete, get by id, get all).
- Mail logging (Mail entity persisted alongside user creation).
- Background job types demonstrated (fire-and-forget, delayed, recurring, continuation).
- Bilingual (en/ar) response messages via JSON-backed localization.
- Centralised error handling through custom exceptions and middleware.

### Architecturally Inconsistent Feature Types

- **Front-end / UI rendering** — this is an API-only project; adding MVC views or Razor Pages would conflict with the architecture.
- **NoSQL / non-relational persistence** — the entire data layer assumes SQL Server + EF Core conventions.
- **Direct DbContext access outside repositories** — bypassing the `IUnitOfWork` / `IBaseRepository` abstraction is inconsistent.
- **Third-party auth providers** (OAuth, OIDC) are not scaffolded; adding them would require substantial new layers.

### Specialized Libraries / Domain Constraints

- `Mapster` + `IRegister` scanning is the designated mapping approach — do not introduce AutoMapper or manual mapping in service layer.
- `Hangfire` is the designated background-job engine — do not introduce `IHostedService`/`BackgroundService` workers for job-like workloads.
- All entities **must** inherit `BaseEntity` to gain `IsDeleted`, `CreationTime`, and `LastModificationTime` audit columns.
