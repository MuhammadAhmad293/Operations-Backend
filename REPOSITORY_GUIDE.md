# Project Overview

The **Meezan Backend** repository is a .NET 6 based Web API built using a Clean Architecture / Layered pattern. It serves as a foundational template or system for handling common backend tasks: standard perations (specifically demonstrated through user management), global exception handling, data mapping, email notifications, and background job processing using Hangfire.

The primary goal of this repository is to demonstrate how to cleanly separate concerns across multiple projects to ensure testability, scalability, and maintainability.

---

# Repository Structure

The solution (`Meezan.sln`) is divided into several strictly purposed `.csproj` projects:

1. **`Meezan` (API Layer):**
   - The entry point of the application. Contains ASP.NET Core Controllers, middlewares (like `ErrorHandlingMiddleware`), Swagger setup, and application configurations (`appsettings.json`, `Program.cs`).
2. **`Meezan.Services` (Application Layer):**
   - Contains the core business logic. It handles data mapping, object validation, and coordinates with repositories to execute the application's use cases (e.g., `UserService`).
3. **`Meezan.IServices`:**
   - Abstractions (interfaces) for the Services layer, enabling Dependency Injection and inversion of control for consumers (the API).
4. **`Meezan.Repositories` (Infrastructure / Data Access Layer):**
   - EF Core implementations. Contains the `AppDbContext`, EF Core migrations, concrete implementations of Repositories, and the `UnitOfWork`.
5. **`Meezan.IRepositories`:**
   - Abstractions (interfaces) for Repositories and the Unit of Work.
6. **`Meezan.DataModel` (Domain Layer):**
   - The core domain model containing the database entities (e.g., `User`, `Mail`). This layer has zero dependencies on other projects.
7. **`Meezan.Dto`:**
   - Data Transfer Objects used to ferry data between the API, Services, and clients without exposing domain models.
8. **`Common`:**
   - Shared cross-cutting utility helpers (e.g., Password Hashing, HTTP clients, Mail Senders, File manipulation).
9. **`Meezan.Ioc` & `EFCoreMigrationExcution`:**
   - Boilerplate or conceptual helpers for Dependency Injection abstractions and Db Migrations wrapper logic. (Core DI resolving logic currently resides in individual `Resolver` classes within their respective projects).

---

# Architecture

The system follows a variation of **Layered / Onion / Clean Architecture**.

### Why this architecture was chosen:

This architecture ensures "Separation of Concerns." By tightly enforcing dependency directions—pointing inward toward the Domain (`DataModel`) and Interfaces—the system isolates the business logic from infrastructure hurdles (databases, APIs). Switching from EF Core to Dapper, or shifting the API structure, would not require rebuilding the business services, as the services interact purely with Contract Interfaces (`IRepositories`).

### Boundaries & Dependency Direction:

Dependencies flow strictly inward:

- **API** depends on **Services** (via interfaces) and **Repositories** (for DI registration).
- **Services** depend on **IServices**, **IRepositories**, **DTOs**, and **DataModel**.
- **Repositories** depend on **IRepositories** and **DataModel**.
- **DataModel** is completely independent.

### Conceptual Architecture

```mermaid
graph TD
    API[API Layer / Controllers] -->|Uses| IService[IServices Abstractions]
    API -->|Registers DI| Resolver[DI Resolvers]

    IService <.. Service[Services / Business Logic] : Extends
    Service -->|Uses| IRep[IRepositories Abstractions]
    Service -->|Uses| DTO[DTOs]
    Service -->|Maps to| Domain[DataModel/Entities]

    IRep <.. Rep[Repositories / Unit of Work] : Extends
    Rep -->|EF Core | DB[(SQL Server)]
    Rep -->|Uses| Domain
```

---

# Request Lifecycle

When a web request arrives, the execution lifecycle follows this path:

1. **Request Pipeline:** The HTTP request hits the API. `ErrorHandlingMiddleware` intercepts it to catch and uniformally format any underlying exceptions.
2. **Controller (e.g., `UserController`):** Acts purely as an orchestrator for HTTP concerns. It receives JSON, binds it to a `UserDto`, and calls corresponding `IUserService` methods.
3. **Service Logic (e.g., `UserService.Add`):**
   - Performs business validation (checks if names/emails exist).
   - Hashes passwords using the `Common.PasswordHash` utility.
   - Maps the incoming `UserDto` to a `User` entity using Mapster.
4. **Data Access via Unit of Work:** The service pushes the action to `UnitOfWork.UserRepository.CreateAsyn(user)`. It does not immediately commit to the DB.
5. **Business Transactions:** Additional actions (like queueing a welcome email to be recorded in the `Mail` table, and invoking `MailSender`) are executed.
6. **Commit:** The Service calls `await UnitOfWork.CommitAsync()` which triggers `AppDbContext.SaveChangesAsync()`, physically writing all transactions into the database together.
7. **Response Formation:** A standardized `ResponseDto` (containing success booleans, messages, or updated DTOs) is passed back to the controller, which returns an HTTP 200 OK.

---

# Domain Model

The domain models are found in **`Meezan.DataModel.Entities`**.
The system employs an **Anemic Domain Model**—this means the entities are primarily POCOs (Plain Old CLR Objects) with getters and setters, used purely to hold state and represent database schema tables. The rich behavior and domain rules instead live inside the `Services`.

- **`User` (Entity):** The main application user definition holding Names, Email, and Hashed Passwords.
- **`Mail` (Entity), `MailType`, `MailStatus`:** Tracking configuration for outgoing platform emails to ensure audit trails on what was sent.
- **`DTOs`:** Used as contracts for what gets sent back and forth between the API consumer avoiding over-posting and accidental data exposure. Responses are generically wrapped in `ResponseDto<T>`.

---

# Data Layer

**Data Access Strategy:**

- **ORM:** Entity Framework Core (version 7 target).
- **Relational DB:** SQL Server.
- **Setup:** The generic `AppDbContext` leverages reflection (`ApplyConfigurationsFromAssembly`) to dynamically attach fluent API configuration files to the models. It singularizes model names into Sql Server table names automatically.

**Repository Pattern & Unit of Work:**
The architecture effectively abstracts EF Core via `IRepository` and `IUnitOfWork`.

- Specific repositories (e.g. `UserRepository`) extend a generic pattern.
- The **`UnitOfWork`** scopes database transactions. When an operation spans multiple repositories (like saving a `User` and a `Mail` state), they share the same EF `DbContext`. Calling `.CommitAsync()` on the Unit of Work executes it efficiently in a single bulk sweep payload.
- To improve application startup performance, the DbContext uses dependency injection configured with a `Lazy<AppDbContext>` implementation (`Lazier.cs`), so that context isn't initiated until database interaction is explicitly needed.

---

# Infrastructure

- **Dependency Injection:** Rather than clogging `Program.cs`, DI declarations are cleanly grouped into specific logic blocks such as `CoreServicesResolver.cs`, `UnitOfWorkResolver.cs`, and `CommonResolver.cs`.
- **Mapping:** Instead of heavy tools like AutoMapper, the project uses **Mapster** to handle performant, compiler-time generated mapping between Entities and DTOs.
- **Background Jobs (Hangfire):** Integrated heavily at the API level (`AddHangfire`, `UseHangfireDashboard`). A database (defined by `HFDBConString`) is maintained automatically by Hangfire to orchestrate long-running asynchronous tasks reliably.
- **Email Delivery:** A custom `MailSender` integrates an SMTP service configured centrally via `appsettings.json`'s `MailSetting` node.
- **Exception Handling:** Centralized globally using `ErrorHandlingMiddleware`. Custom Exceptions (`NameRequiredException`, `InvalidRequestException`) thrown from within `Services` are intercepted and flattened into 400 Bad Requests automatically.

---

# Dependency Graph

```text
Meezan (API / Host)
 ├──> Meezan.Services (App Logic)
 │    ├──> Meezan.IServices
 │    ├──> Meezan.Repositories
 │    ├──> Meezan.DataModel
 │    └──> Common
 ├──> Meezan.Repositories (EF Data Layer)
 │    ├──> Meezan.IRepositories
 │    ├──> Meezan.DataModel
 │    └──> Common
 ├──> Meezan.Dto
 └──> Common
```

---

# Key Design Patterns

1. **Dependency Injection & Inversion of Control:** Core principle. Higher-level modules (Controllers) don’t depend on lower-level modules (Repositories directly); they depend on interfaces like `IUserService`.
2. **Repository & Unit of Work:** Wraps the ORM (EF Core) to ensure atomic tracking of aggregate actions. Gives flexibility to execute tests mocking Repositories without needing an EF Memory DB.
3. **Lazy Init Injection:** `Lazy<T>` pattern implemented via `Lazier.cs` to defer DbContext creation until required to preserve API pipeline startup speed.
4. **Global Exception Handling (Middleware):** Utilizing custom middlewares to keep Controllers free of try/catch spam.
5. **DTO Pattern:** Enforces a rigid structure over what HTTP clients receive and send, separate from domain logic.
6. **Options Pattern:** Leveraging `appsettings.json` strongly bound via `builder.Configuration.Bind` onto classes like `MailSettings` injected via Singleton.

---

# Important Files Explained

- **`Program.cs`:** The entry point bootstrap. Wires up settings mappings, calls the application's local resolvers (`CoreServicesResolver`), binds Hangfire, and scaffolds CORS and Swagger configuration.
- **`UnitOfWork.cs`:** Located in `Meezan.Repositories.UnitOfWork`. It controls all actual concrete data persistence. If logic requires Db saving, it goes through here via `CommitAsync()`.
- **`UserService.cs`:** Located in `Meezan.Services.UserService`. Representative file demonstrating standard application execution. Shows input validation throwing custom exceptions, Mapster usage, password hashing implementation, saving data through the Unit of Work, and generating side-effect models like sending EMails.
- **`AppDbContext.cs`:** Maps the application's Object Model to the Database. Configures the specific ModelBuilder parameters, dynamic naming structures, and kicks off initial data seeding processes.
- **`ErrorHandlingMiddleware.cs`:** Wraps all API responses tracking Exceptions mapping specific exceptions to strict HTTP status codes (404 for object not found, 400 for bad parameters).

---

# How To Extend The System

To quickly introduce a new entity and feature into this solution, follow these steps:

1. **Domain:** Create your POCO model inside `Meezan.DataModel.Entities`.
2. **Database Schema:** Formulate any complex configuration for the Entity via an `IEntityTypeConfiguration` in the context project. Generate your EF Core migration.
3. **DTOs:** Define your Request Models and Response Models inside `Meezan.Dto.DTOs`.
4. **Interfaces:** Create `IMyEntityRepository` in `Meezan.IRepositories` and `IMyEntityService` in `Meezan.IServices`.
5. **Repositories:** Create your concrete repository in `Meezan.Repositories`. Hook it up to `UnitOfWork` and `IUnitOfWork`.
6. **Business Service:** Create your concrete service within `Meezan.Services`. Ensure you map validations, map via Mapster, and call `UnitOfWork.CommitAsync()`. Remember to register this Service inside `CoreServicesResolver.cs` if dependency injection fails runtime!
7. **Controller:** Scaffold a new Web API Controller in `Meezan.Controllers` referencing solely your newly scoped `IMyEntityService` through its constructor.

---

# Glossary of Important Concepts

- **Unit Of Work (`CommitAsync`)**: Grouping multiple statements. Unless you call `CommitAsync`, changes are tracked but not saved to the DB.
- **Mapster**: A convention-based object-to-object mapper focused on speed. Scans and compiles relationships automatically.
- **ResponseDto**: A standardized API envelope ensuring that all endpoints return consistent formatting (`{ Status, Message, Data }`).

---

# Summary for Future AI Agents

**Agent Directive:** This repository is an N-Tier .NET 6 Clean Architecture backend template.

- **The DB approach** is Code-First Entity Framework Core using a standard Repository + Unit of Work implementation.
- **The Business Logic** is strictly housed inside the `Services` project. **Never** place queries, `DbContext` access, or validation code directly in `Controllers`.
- **Data flow rule:** API Controller => IServices (DTOs) => Mapster (DTO to Entity) => UnitOfWork (Entities) => SaveChanges => Mapster (Entity to DTO) => returned as `ResponseDto<T>`.
- **Component discovery:** When asked to extend logic, you must touch `DataModel` for structural schema limits, `Dto` for API payloads, `IServices/Services` for feature handling, and `IRepositories/Repositories` for CRUD definitions. All interface DI bindings are managed by static Resolver classes rather than raw initialization in `Program.cs`.
