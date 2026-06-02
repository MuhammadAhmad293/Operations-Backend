# Domain Deep Dive: Data Layer

## Overview
The data layer uses Entity Framework Core 10 with a SQL Server provider, wired through the Unit of Work pattern. Repositories are generic, lazy-loaded, and surfaced exclusively through `IUnitOfWork`.

---

## Entity Hierarchy

All entities extend `BaseEntity`:
```csharp
public class BaseEntity
{
    public bool IsDeleted { get; set; }
    public DateTime CreationTime { get; set; }
    public DateTime LastModificationTime { get; set; }
}
```

Multilingual lookup/reference entities extend `BaseMultilingualTextEntity`:
```csharp
public class BaseMultilingualTextEntity : BaseEntity
{
    public string EnName { get; set; }
    public string ArName { get; set; }
    public string EnDescription { get; set; }
    public string ArDescription { get; set; }
}
```

Used by: `MailStatus`, `MailType`.

---

## AppDbContext

`AppDbContext` applies two global conventions on `OnModelCreating`:
1. **SingularizeTableNames** — overrides EF Core's default pluralised table naming, mapping each entity to its own class name as the table name.
2. **ApplyConfigurationsFromAssembly** — scans the assembly for all `IEntityTypeConfiguration<T>` classes and applies them.
3. **SeedInitialData** — seeds reference data (MailStatus, MailType) via model builder.

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    SingularizeTableNames(modelBuilder);
    ModelConfiguration(modelBuilder);
    base.OnModelCreating(modelBuilder);
    modelBuilder.SeedInitialData();
}
```

---

## Lazy DbContext Injection

`AppDbContext` is injected as `Lazy<AppDbContext>` everywhere it is needed. This is enabled by a custom `Lazier<T>` class:
```csharp
internal class Lazier<T> : Lazy<T> where T : class
{
    public Lazier(IServiceProvider provider)
        : base(() => provider.GetRequiredService<T>())
    {
    }
}
```

Registered in `UnitOfWorkResolver.ResolveLazier`:
```csharp
services.AddScoped(typeof(Lazy<>), typeof(Lazier<>));
```

---

## BaseRepository

All repositories extend `BaseRepository<T>`:
```csharp
public abstract class BaseRepository<T> : IBaseRepository<T> where T : class
{
    public Lazy<AppDbContext> AppDbContext { get; }

    public void CreateAsyn(T entity) => AppDbContext.Value.Set<T>().AddAsync(entity);
    public void Update(T entity) => AppDbContext.Value.Set<T>().Update(entity);
    public void Delete(T entity) => AppDbContext.Value.Set<T>().Remove(entity);

    public async Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> filter)
    {
        IQueryable<T> query = AppDbContext.Value.Set<T>();
        if (filter != null) query = query.Where(filter);
        return await query.FirstOrDefaultAsync();
    }

    public async Task<List<T>> GetAllAsync() => await AppDbContext.Value.Set<T>().ToListAsync();
}
```

Concrete repositories only pass the `Lazy<AppDbContext>` to the base constructor:
```csharp
public class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(Lazy<AppDbContext> appDbContext) : base(appDbContext) { }
}
```

---

## Unit of Work

`UnitOfWork` holds a shared `Lazy<AppDbContext>` and exposes repositories as properties (instantiated fresh on each access):
```csharp
public IUserRepository UserRepository => new UserRepository(AppDbContext);
public IMailRepository MailRepository => new MailRepository(AppDbContext);
public Task<int> CommitAsync() => AppDbContext.Value.SaveChangesAsync();
```

---

## Entity Configuration

Every entity has an `IEntityTypeConfiguration<T>` that applies SQL server defaults for the audit columns:
```csharp
internal class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(e => e.CreationTime).HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.LastModificationTime).HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);
    }
}
```

---

## Data Seeding

Reference data (lookup tables) is seeded inline in `InitialDataSeeding` (an extension method on `ModelBuilder`). Seed records use static `DateTime` values to avoid migration churn. Enum values drive the primary keys:
```csharp
modelBuilder.Entity<MailStatus>().HasData(
    new MailStatus
    {
        MailStatusId = (int)MailStatusEnum.New,
        EnName = "New", ArName = "جديد",
        CreationTime = new DateTime(2023, 01, 1)
    }, ...
);
```

---

## Migrations

EF Core migrations are stored in `Operations.Repositories/Migrations/`. A separate `EFCoreMigrationExcution` project exists as a standalone console app to apply migrations independently from the API process.

---

## Key Constraints
- `UseLazyLoadingProxies(false)` — no auto-loading of navigation properties.
- SQL Server only — `UseSqlServer` is hard-coded in `UnitOfWorkResolver`.
- Table names are singular — do not override.
- `CreateAsyn` does **not** await the `AddAsync` call; commit is deferred to `CommitAsync()`.
