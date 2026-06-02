# Style Guide: base-repositories

## Unique Conventions

### Lazy DbContext as Protected Property
The base class exposes `AppDbContext` as a `public Lazy<AppDbContext>` property (not a field), accessible to derived classes:
```csharp
public Lazy<AppDbContext> AppDbContext { get; }
internal BaseRepository(Lazy<AppDbContext> appDbContext) => AppDbContext = appDbContext;
```

### Generic Constraint
The constraint is `where T : class` only — no `IEntity` or `BaseEntity` constraint, allowing the base to be used with any EF entity type.

### Expression-Based Filtering
Read methods use `Expression<Func<T, bool>>` predicates, not LINQ method chaining in the base:
```csharp
public async Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> filter)
{
    IQueryable<T> query = AppDbContext.Value.Set<T>();
    if (filter != null) query = query.Where(filter);
    return await query.FirstOrDefaultAsync();
}
```
