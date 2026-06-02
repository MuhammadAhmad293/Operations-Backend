# Style Guide: entities

## Unique Conventions

### Mandatory Base Class
Every entity **must** inherit `BaseEntity`:
```csharp
public class User : BaseEntity
{
    public int Id { get; set; }
    // ...
}
```

### Multilingual Lookup Entities
Reference/lookup entities (like `MailStatus`, `MailType`) inherit `BaseMultilingualTextEntity` which itself extends `BaseEntity`, adding bilingual name and description columns:
```csharp
public class MailStatus : BaseMultilingualTextEntity
{
    public int MailStatusId { get; set; }
    // ...
}
```

### Primary Key Naming
The PK follows the pattern `{EntityName}Id` for lookup entities (e.g., `MailStatusId`, `MailTypeId`, `MailId`). For the main User entity, the PK is simply `Id`.

### Navigation Property Ownership
Navigation properties are declared without `virtual` (lazy loading is disabled). Collections use `ICollection<T>`:
```csharp
public ICollection<MailAttachment> Attachments { get; set; }
public MailStatus MailStatus { get; set; }
```

### No Data Annotations
Zero use of `[Required]`, `[MaxLength]`, etc. All constraints are defined through EF Core Fluent API in `EntityConfiguration` classes.

### No Constructor Initialisation
Entities do not initialise properties in constructors. All property assignment happens in the service/repository layer.
