# Style Guide: enums

## Unique Conventions

### Used as Typed Primary Keys
Enums are used to provide named aliases for integer primary keys in lookup tables. The enum value is cast to int when assigning FKs:
```csharp
MailStatusId = (int)MailStatusEnum.New,
MailTypeId = (int)MailTypeEnum.WelcomeMail,
```

### Used for ResponseStatus Branching
`ResponseStatus` is used with the `is` pattern in conditional checks, not `==`:
```csharp
if (response.Status is ResponseStatus.Error)
    return response;
```

### Location
All enums live in `Common/Enums/` except where tightly coupled to a specific project (none currently). This makes them accessible across all layers without a circular project reference.

### PascalCase Values
Enum members use PascalCase: `ResponseStatus.Success`, `MailStatusEnum.New`, `SortDirection.Ascending`.
