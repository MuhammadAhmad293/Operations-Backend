# Domain Deep Dive: Service Layer

## Overview

The service layer contains all business logic. Every service inherits `BaseService`, which provides the three shared infrastructure concerns: `IUnitOfWork`, `IMapper` (Mapster), and `ILocalizationService`.

---

## BaseService

```csharp
public class BaseService
{
    protected IUnitOfWork UnitOfWork { get; }
    protected IMapper Mapper { get; }
    public ILocalizationService Localization { get; }

    public BaseService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService)
    {
        UnitOfWork = unitOfWork;
        Mapper = mapper;
        Localization = localizationService;
    }
}
```

All concrete services extend this and add their own dependencies (e.g., `IPasswordHash`, `IMailSender`).

---

## ResponseDto Pattern

All public service methods return `ResponseDto<T>`. The result is always built via the fluent chain methods:

```csharp
// initialise (starts in Error state)
ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();

// on success
return response.GetSuccessResponse(Localization.GeneralSuccess);

// on failure
return response.GetErrorResponse(Localization.GeneralError);

// with typed data payload
return response.GetSuccessResponse(MapUserDto(user));
```

`ResponseDto<T>` carries:

- `Status` (`ResponseStatus.Success` or `ResponseStatus.Error`)
- `Message` (nullable string, localised)
- `Data` (typed result payload, null on error)

---

## Input Validation via Custom Exceptions

Services validate inputs by throwing typed exceptions — not by setting the response to Error:

```csharp
if (string.IsNullOrWhiteSpace(userDto.FirstName))
    throw new NameRequiredException("Please enter first name");

if (string.IsNullOrWhiteSpace(userDto.Email))
    throw new InvalidRequestException("Please enter email");
```

`ErrorHandlingMiddleware` intercepts these and returns the appropriate HTTP status code + serialised message.

---

## Write Flow (Create / Update / Delete)

All writes follow the same three-step pattern:

1. Stage the operation on the repository (no await — repositories are sync-enqueue).
2. Optionally stage related Meezan (e.g., Mail entity).
3. Commit everything in one call — check the affected-rows count:

```csharp
UnitOfWork.UserRepository.CreateAsyn(user);
UnitOfWork.MailRepository.CreateAsyn(mail);

if (await UnitOfWork.CommitAsync() > default(int))
    return response.GetSuccessResponse(Localization.GeneralSuccess);

return response.GetErrorResponse(Localization.GeneralError);
```

---

## Mapster Mapping

Object mapping uses Mapster. The `IMapper` (injected via `BaseService`) is used for simple cases:

```csharp
User user = Mapper.Map<User>(request);
```

Custom mappings are registered via `IRegister` in `Meezan.Services/Mapper/`:

```csharp
public class UserMapper : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<UserDto, User>();
    }
}
```

The assembly is scanned in `CoreServicesResolver.ResolveMapper()`.

> Note: `UserService` also contains static private `MapUser` / `MapUserDto` helper methods for the inverse (Entity → DTO) direction. These co-exist with Mapster for now.

---

## Password Hashing

Passwords are hashed using `IPasswordHash` (injected) before storage:

```csharp
user.Password = PasswordHash.CreateHash(request.Password);
```

`PasswordHash.CreateHash` uses PBKDF2 (50 iterations, 10-byte salt, 10-byte hash), returning a colon-delimited string: `iterations:salt:hash`.

---

## DI Registration

Services are registered as Scoped in `CoreServicesResolver.ResolveCoreServices`:

```csharp
services.AddScoped<IUserService, UserService.UserService>();
services.AddScoped<ILocalizationService, LocalizationService>();
services.AddScoped<IJobService, JobService>();
```

`IPasswordHash` is registered as Singleton (stateless utility).
