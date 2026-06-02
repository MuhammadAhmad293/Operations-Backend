# Style Guide: entity-configurations

## Unique Conventions

### Internal Visibility
All entity configuration classes are `internal` (not `public`), since they are only consumed by EF Core's assembly scanner:
```csharp
internal class UserConfiguration : IEntityTypeConfiguration<User>
```

### Audit Column Defaults on Every Entity
Every entity configuration must set SQL defaults for the three `BaseEntity` audit columns:
```csharp
builder.Property(e => e.CreationTime).HasDefaultValueSql("GETDATE()");
builder.Property(e => e.LastModificationTime).HasDefaultValueSql("GETDATE()");
builder.Property(e => e.IsDeleted).HasDefaultValue(false);
```

### No Explicit Table Name
Table naming is handled globally by `AppDbContext.SingularizeTableNames`; individual configurations do not call `builder.ToTable(...)`.

### Minimal Configuration
Configurations only set what EF Core cannot derive from conventions — primarily the SQL defaults for audit columns and any relationship/constraint specifics. They do not restate types, column names, or lengths that match conventions.
