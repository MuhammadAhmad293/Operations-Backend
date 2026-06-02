# Style Guide: repositories

## Unique Conventions

### Minimal Concrete Class
Concrete repositories contain only a constructor that passes `Lazy<AppDbContext>` to `BaseRepository<T>`:
```csharp
public class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(Lazy<AppDbContext> appDbContext) : base(appDbContext) { }
}
```

Custom query methods are only added to concrete repositories when they cannot be expressed via the inherited `FirstOrDefaultAsync` + `Expression<Func<T, bool>>`.

### No Direct DbContext in Repositories
All database access goes through `AppDbContext.Value.Set<T>()`. The `Value` property is accessed only when a database call is actually made (lazy materialisation):
```csharp
public void CreateAsyn(T entity) => AppDbContext.Value.Set<T>().AddAsync(entity);
```

### Sync-Enqueue for Write Operations
`CreateAsyn`, `Update`, and `Delete` do not await — they enqueue the change tracker operation. Only `CommitAsync` (on UnitOfWork) is awaited:
```csharp
public void CreateAsyn(T entity) => AppDbContext.Value.Set<T>().AddAsync(entity);
public void Update(T entity) => AppDbContext.Value.Set<T>().Update(entity);
public void Delete(T entity) => AppDbContext.Value.Set<T>().Remove(entity);
```

### IRepository Location
Repository interfaces live in `Operations.IRepositories/IRepository/`. They extend `IBaseRepository<T>` and only add entity-specific query methods:
```csharp
public interface IUserRepository : IBaseRepository<User> { }
```
