# Style Guide: api-controllers

## Unique Conventions

### Class Declaration
Controllers always use `ControllerBase` (never `Controller`). Both attributes are always present together:
```csharp
[Route("api/[controller]")]
[ApiController]
public class UserController : ControllerBase
```

### Service Injection — Property, Not Field
The injected service is stored as a **private property**, not a private readonly field:
```csharp
private IUserService UserService { get; }
public UserController(IUserService userService) => UserService = userService;
```

### Action Return Type
All actions return `Task<IActionResult>` regardless of the shape of the response. Every action body is a single `return Ok(await Service.Method(...))` statement with no extra logic:
```csharp
[HttpPost]
public async Task<IActionResult> Add(UserDto userDto)
    => Ok(await UserService.Add(userDto));
```

### No Try/Catch
Controllers never contain try/catch blocks. All exception handling is delegated to `ErrorHandlingMiddleware`.

### Route Specificity
Sub-routes beyond the HTTP verb are only added when disambiguation is required (e.g., `[HttpGet("order/{id:int}")]`). They are not used by default.

### Job Controllers
When a controller dispatches Hangfire jobs, all three Hangfire types (`IJobService`, `IBackgroundJobClient`, `IRecurringJobManager`) are constructor-injected and stored as `private readonly` fields:
```csharp
private readonly IJobService JobService;
private readonly IBackgroundJobClient BackgroundJobClient;
private readonly IRecurringJobManager RecurringJobManager;
```
