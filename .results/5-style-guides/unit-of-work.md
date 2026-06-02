# Style Guide: unit-of-work

## Unique Conventions

### Repository Properties as New Instantiations
`UnitOfWork` does not cache repository instances. Each access to a repository property creates a new repository object, passing the shared `Lazy<AppDbContext>`:
```csharp
public IUserRepository UserRepository => new UserRepository(AppDbContext);
public IMailRepository MailRepository => new MailRepository(AppDbContext);
```

This works correctly because the underlying `Lazy<AppDbContext>` is shared, so all repositories within the same scope operate on the same `AppDbContext` instance.

### Commit as SaveChanges Proxy
`CommitAsync()` is a direct delegate to `AppDbContext.Value.SaveChangesAsync()`:
```csharp
public Task<int> CommitAsync() => AppDbContext.Value.SaveChangesAsync();
```

### Empty Dispose
`Dispose()` is implemented but empty. DbContext lifetime is managed by the DI container (Scoped).

### IUnitOfWork Interface
The interface declares `CommitAsync()` as the only method, and exposes each repository as a named property:
```csharp
public interface IUnitOfWork : IDisposable
{
    Task<int> CommitAsync();
    IUserRepository UserRepository { get; }
    IMailRepository MailRepository { get; }
}
```

### Repository Sections
Both the interface and implementation group repository properties under `#region IRepository` / `#region Repository Implementation`.
