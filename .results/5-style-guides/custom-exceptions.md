# Style Guide: custom-exceptions

## Unique Conventions

### Minimal Class Bodies

Each exception class contains only a constructor with a default message value:

```csharp
public class InvalidRequestException : Exception
{
    public InvalidRequestException(string message = "Invalid Request") : base(message) { }
}
```

No additional properties, methods, or constructors.

### Default Message as Documentation

The default message string doubles as the canonical name for the error type. It is meaningful and human-readable.

### Exception Hierarchy

All custom exceptions extend `Exception` directly — no intermediate base exception class.

### Location

All custom exceptions live in `Meezan.Services/CustomExceptions/`. They are not in `Common` because they are domain-specific and used only by the service layer.

### Thrown, Not Returned

Exceptions are thrown from validation logic; they are never wrapped in `ResponseDto` or returned as values. The middleware handles them and converts to HTTP responses.

### Three Types, Mapped in Middleware

Current types and their HTTP mappings (defined in `ErrorHandlingMiddleware`):

- `ObjectNotFoundException` → 404 Not Found
- `NameRequiredException` → 400 Bad Request
- `InvalidRequestException` → 400 Bad Request
