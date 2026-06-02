# Style Guide: swagger-filters

## Unique Conventions

### Interface
Filters implement `IOperationFilter` from `Swashbuckle.AspNetCore.SwaggerGen`.

### Null Guard on Parameters
The `Apply` method always null-checks `operation.Parameters` and initialises it before adding:
```csharp
if (operation.Parameters is null)
    operation.Parameters = new List<OpenApiParameter>();
```

### Purpose
The only filter in the project (`SwaggerHeaderFilter`) adds a global `Accept-Language` header to every operation. This drives the bilingual localization system. New filters should follow this pattern of a single focused concern.

### Registration
Registered globally via `c.OperationFilter<T>()` in `AddSwaggerGen`, not on individual controllers or actions:
```csharp
builder.Services.AddSwaggerGen(c => { c.OperationFilter<SwaggerHeaderFilter>(); });
```
