# Style Guide: lazy-loading-helpers

## Unique Conventions

### Internal Visibility

`Lazier<T>` is `internal` — it is an implementation detail of the `Meezan.Repositories` project and not exposed publicly.

### Provider-Based Resolution

The lazy factory uses `IServiceProvider.GetRequiredService<T>()` rather than a manually passed factory:

```csharp
internal class Lazier<T> : Lazy<T> where T : class
{
    public Lazier(IServiceProvider provider)
        : base(() => provider.GetRequiredService<T>())
    {
    }
}
```

### Registered as Open Generic

Registered with the open generic form to support any `Lazy<T>` across the entire application:

```csharp
services.AddScoped(typeof(Lazy<>), typeof(Lazier<>));
```

This allows `AppDbContext` to be injected as `Lazy<AppDbContext>` anywhere without additional registration.
