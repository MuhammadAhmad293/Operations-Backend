# Style Guide: base-entities

## Unique Conventions

### BaseEntity is the Universal Root
Every entity in the project extends `BaseEntity`. No entity bypasses this:
```csharp
public class BaseEntity
{
    public bool IsDeleted { get; set; }
    public DateTime CreationTime { get; set; }
    public DateTime LastModificationTime { get; set; }
}
```

### Soft Delete via IsDeleted
`IsDeleted` is present on every entity but is not automatically filtered by the repository. Query filtering (where IsDeleted == false) is the caller's responsibility.

### BaseMultilingualTextEntity for Lookup Tables
Reference/lookup entities (MailStatus, MailType) extend `BaseMultilingualTextEntity` to add bilingual support:
```csharp
public class BaseMultilingualTextEntity : BaseEntity
{
    public string EnName { get; set; }
    public string ArName { get; set; }
    public string EnDescription { get; set; }
    public string ArDescription { get; set; }
}
```

### No Interfaces on Base Entities
Base entities do not implement `IEntity` or any marker interface. Identity tracking is handled entirely by EF Core conventions and the `IEntityTypeConfiguration` classes.
