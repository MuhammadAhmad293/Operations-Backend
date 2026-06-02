# Style Guide: middleware

## Unique Conventions

### Constructor Pattern
Middleware receives `RequestDelegate next` in its constructor and stores it as a private field named `next` (lowercase):
```csharp
private readonly RequestDelegate next;
public ErrorHandlingMiddleware(RequestDelegate next) => this.next = next;
```

### Invoke Signature
The `Invoke` method accepts `HttpContext context` and no other parameters (despite the comment `/* other dependencies */` which is a placeholder):
```csharp
public async Task Invoke(HttpContext context)
{
    try { await next(context); }
    catch (Exception ex) { await HandleExceptionAsync(context, ex); }
}
```

### Exception-to-Status Mapping Pattern
The mapping method is `private static` and resolves status codes by checking exception type via `is`:
```csharp
private static Task HandleExceptionAsync(HttpContext context, Exception ex)
{
    var code = HttpStatusCode.InternalServerError;
    if (ex is ObjectNotFoundException) code = HttpStatusCode.NotFound;
    else if (ex is NameRequiredException || ex is ArgumentNullException || ex is InvalidRequestException)
        code = HttpStatusCode.BadRequest;
    ...
}
```

### Error Response Body
The error body is `JsonConvert.SerializeObject(ex.Message)` — a JSON string (not an object). Content-Type is always `"application/json"`:
```csharp
var result = JsonConvert.SerializeObject(ex.Message);
context.Response.ContentType = "application/json";
context.Response.StatusCode = (int)code;
return context.Response.WriteAsync(result);
```

### Registration
Middleware is registered in `Program.cs` using the type parameter overload:
```csharp
app.UseMiddleware(typeof(ErrorHandlingMiddleware));
```
