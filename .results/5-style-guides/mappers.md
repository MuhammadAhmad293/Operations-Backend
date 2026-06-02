# Style Guide: mappers

## Unique Conventions

### IRegister Pattern
All Mapster mappings use the `IRegister` interface. Each mapper class registers one or more type mappings in its `Register` method:
```csharp
public class UserMapper : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<UserDto, User>();
    }
}
```

### Assembly Scanning
Mapper registrations are discovered via `TypeAdapterConfig.GlobalSettings.Scan(Assembly.GetExecutingAssembly())` in `CoreServicesResolver.ResolveMapper`. No manual registration of individual mappers.

### One Mapper Per Domain Entity
Each domain entity has at most one `IRegister` class in `Operations.Services/Mapper/`.

### Complementary Static Helpers in Services
Inverse mappings (Entity → DTO) that require custom projection logic are implemented as `private static` helper methods directly in the service class rather than in a separate mapper file. This is a project-specific pattern, not an architectural smell.
