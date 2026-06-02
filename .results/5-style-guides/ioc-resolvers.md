# Style Guide: ioc-resolvers

## Unique Conventions

### Static Classes with Named Static Methods
Resolvers are always `public static class` with `public static void` methods:
```csharp
public static class CoreServicesResolver
{
    public static void ResolveCoreServices(IServiceCollection services, IConfiguration configuration) { ... }
    public static void ResolveMapper(IServiceCollection services) { ... }
}
```

### Method Naming: `Resolve*`
All resolver methods start with `Resolve` followed by the concern they register (e.g., `ResolveCoreServices`, `ResolveMapper`, `ResolveUintOfWork`, `ResolveLazier`, `ResolveCommonServices`).

### Signature Convention
Methods that need the connection string receive both `IServiceCollection` and `IConfiguration`. Methods that only register services receive only `IServiceCollection`.

### One Resolver per Project Boundary
Each project that owns registrations has its own resolver. `Operations.Services` → `CoreServicesResolver`, `Operations.Repositories` → `UnitOfWorkResolver`, `Common` → `CommonResolver`.

### No Service Locator
Resolver methods never call `services.BuildServiceProvider()` or resolve services themselves. They only register.
