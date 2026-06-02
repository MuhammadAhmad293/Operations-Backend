# Style Guide: settings-models

## Unique Conventions

### POCO Binding Model
Settings classes are plain POCOs with auto-properties, no validation attributes, and no constructors. They are bound from `appsettings.json` using `builder.Configuration.Bind(sectionName, instance)`:
```csharp
MailSettings MailSetting = new();
builder.Configuration.Bind("MailSetting", MailSetting);
builder.Services.AddSingleton(MailSetting);
```

### Singleton Registration as Concrete Type
Settings are registered as Singleton using the concrete type (not an interface):
```csharp
builder.Services.AddSingleton(MailSetting);
```

Services receive the concrete type via constructor injection.

### Dual Registration
In `Program.cs`, `MailSettings` is bound and registered twice (once as `MailSettings`, once as `MailSetting` — same type, same section). This is a current project quirk; new settings should be bound and registered once.
