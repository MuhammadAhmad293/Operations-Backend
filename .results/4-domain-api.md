# Domain Deep Dive: API (Controllers, Middleware, Filters)

## Overview
The API domain is the HTTP entry point. It is implemented as ASP.NET Core 10 Web API using the minimal hosting model. All controllers are thin delegators that forward requests to service-layer interfaces.

---

## Controllers

### Pattern
All controllers follow the same structure:
- Extend `ControllerBase` (never `Controller` — no view support needed).
- Decorated with `[Route("api/[controller]")]` and `[ApiController]`.
- Constructor-inject exactly one `I*Service` into a `private` property (not a field).
- Every action returns `Task<IActionResult>` and wraps the service result with `Ok(...)`.

### Example — `UserController`
```csharp
[Route("api/[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private IUserService UserService { get; }
    public UserController(IUserService userService) => UserService = userService;

    [HttpPost]
    public async Task<IActionResult> Add(UserDto userDto)
        => Ok(await UserService.Add(userDto));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
        => Ok(await UserService.Delete(id));
}
```

### Example — `JobTestController`
Dispatches background work via Hangfire clients injected into the constructor:
```csharp
public JobTestController(IJobService jobService, IBackgroundJobClient backgroundJobClient,
    IRecurringJobManager recurringJobManager) { ... }

[HttpGet("reccuringJob")]
public ActionResult CreateReccuringJob()
{
    RecurringJobManager.AddOrUpdate("jobId", () => JobService.ReccuringJob(), Cron.Minutely);
    return Ok();
}
```

---

## Error Handling Middleware

`ErrorHandlingMiddleware` wraps the entire pipeline. It catches unhandled exceptions and maps custom types to HTTP status codes:

| Exception Type            | HTTP Status |
|---------------------------|-------------|
| `ObjectNotFoundException` | 404         |
| `NameRequiredException`   | 400         |
| `InvalidRequestException` | 400         |
| All other exceptions      | 500         |

```csharp
private static Task HandleExceptionAsync(HttpContext context, Exception ex)
{
    var code = HttpStatusCode.InternalServerError;
    if (ex is ObjectNotFoundException) code = HttpStatusCode.NotFound;
    else if (ex is NameRequiredException || ex is ArgumentNullException || ex is InvalidRequestException)
        code = HttpStatusCode.BadRequest;

    var result = JsonConvert.SerializeObject(ex.Message);
    context.Response.ContentType = "application/json";
    context.Response.StatusCode = (int)code;
    return context.Response.WriteAsync(result);
}
```

> Error bodies are a JSON-serialised string (the exception message), not a structured object.

---

## Swagger Filter

`SwaggerHeaderFilter` implements `IOperationFilter` to inject an `Accept-Language` header parameter on every Swagger operation. This supports the bilingual (en/ar) localization system.

```csharp
operation.Parameters.Add(new OpenApiParameter
{
    Name = "Accept-Language",
    In = ParameterLocation.Header,
    Schema = new OpenApiSchema { Type = "string" }
});
```

Registered in `Program.cs`:
```csharp
builder.Services.AddSwaggerGen(c => { c.OperationFilter<SwaggerHeaderFilter>(); });
```

---

## CORS Configuration
CORS is configured once in `Program.cs` to allow any method/header from `http://localhost:4200` (expected Angular front-end origin). No per-controller overrides exist.

---

## Key Constraints
- Controllers never contain try/catch — the middleware handles all exceptions.
- No business logic, mapping, or validation in controllers.
- Swagger is only exposed in `Development` environment.
- Hangfire dashboard is exposed at `/hangfire` with no authentication guard (development only).
