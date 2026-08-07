# Domain Deep Dive: IoC Composition (Resolver Pattern)

## Overview

Dependency injection is wired through static `Resolver` classes, one per project. There is no attribute-based or convention-based scanning for DI. `Program.cs` calls each resolver explicitly.

---

## Resolver Classes

| Resolver               | Location                        | Responsibility                                     |
| ---------------------- | ------------------------------- | -------------------------------------------------- |
| `CoreServicesResolver` | `Meezan.Services/Resolver/`     | Services (Scoped), Localization (Scoped), Mapster  |
| `UnitOfWorkResolver`   | `Meezan.Repositories/Resolver/` | DbContext, IUnitOfWork (Scoped), Lazy<> helper     |
| `CommonResolver`       | `Common/Resolver/`              | Mail, File, Validator, HttpClient (all Scoped)     |
| `IocManager`           | `Meezan.Ioc/`                   | Aggregates Repo resolvers (thin wrapper, optional) |

---

## Program.cs Wiring Order

```csharp
builder.Services.AddSingleton<IPasswordHash, PasswordHash>();

CoreServicesResolver.ResolveCoreServices(builder.Services, builder.Configuration);
CoreServicesResolver.ResolveMapper(builder.Services);
CommonResolver.ResolveCommonServices(builder.Services, builder.Configuration);
UnitOfWorkResolver.ResolveUintOfWork(builder.Services, builder.Configuration);
UnitOfWorkResolver.ResolveLazier(builder.Services, builder.Configuration);
```

---

## Mapster Registration

Mapster is registered in two steps inside `CoreServicesResolver.ResolveMapper`:

```csharp
TypeAdapterConfig config = TypeAdapterConfig.GlobalSettings;
config.Scan(Assembly.GetExecutingAssembly());   // discovers all IRegister implementations
services.AddSingleton(config);
services.AddScoped<IMapper, ServiceMapper>();
```

`Assembly.GetExecutingAssembly()` targets `Meezan.Services`, so all `IRegister` classes in that project are auto-discovered.

---

## Lifetime Summary

| Registration           | Lifetime  |
| ---------------------- | --------- |
| `IPasswordHash`        | Singleton |
| `MailSettings`         | Singleton |
| `TypeAdapterConfig`    | Singleton |
| `IUserService`         | Scoped    |
| `ILocalizationService` | Scoped    |
| `IJobService`          | Scoped    |
| `IMapper`              | Scoped    |
| `IUnitOfWork`          | Scoped    |
| `Lazy<>`               | Scoped    |
| `IMailSender`          | Scoped    |
| `IFileHelper`          | Scoped    |
| `IValidatorHelper`     | Scoped    |
| `IHttpClientHelper`    | Scoped    |
| `IHttpContextAccessor` | Transient |
| `ClaimsPrincipal`      | Transient |

---

## Key Constraints

- Use constructor injection only — no `[FromServices]`, no `IServiceProvider.GetService` in application code.
- Each project that owns registrations has its own `Resolver` class — do not register cross-project services in a foreign resolver.
- `Lazy<>` is registered globally via `Lazier<T>` to support lazy DbContext injection in repositories.
