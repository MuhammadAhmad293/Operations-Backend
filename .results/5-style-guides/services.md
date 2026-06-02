# Style Guide: services

## Unique Conventions

### Mandatory BaseService Inheritance
Every service extends `BaseService` and passes the three shared dependencies to `base(...)`:
```csharp
public class UserService : BaseService, IUserService
{
    public UserService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localization,
        IPasswordHash passwordHash, IMailSender mailSender, MailSettings mailSetting)
        : base(unitOfWork, mapper, localization)
    {
        PasswordHash = passwordHash;
        // ...
    }
}
```

### #region Grouping
Service methods are grouped with `#region Public Methods` and `#region Private Methods`:
```csharp
#region Public Methods
public async Task<ResponseDto<EmptyResponseDto>> Add(...) { ... }
#endregion

#region Private Methods
private ResponseDto<EmptyResponseDto> ValidateUser(...) { ... }
#endregion
```

### ResponseDto Fluent Initialisation
Every public method starts by creating the response in Error state:
```csharp
ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse(Localization.GeneralError);
```

### Commit Check Pattern
All write operations check `CommitAsync()` return value with `> default(int)`:
```csharp
return await UnitOfWork.CommitAsync() > default(int)
    ? response.GetSuccessResponse()
    : response.GetErrorResponse();
```

### Private Mapping Helpers
Services define `private static` methods for entity↔DTO mapping when the Mapster projection isn't used directly:
```csharp
private static UserDto MapUserDto(User user) { ... }
private static List<UserDto> MapUserDtos(List<User> users) { ... }
private static void MapUser(User user, UserDto userDto) { ... }
```

### Validation as Guard Clauses
Input validation runs first; any validation failure either throws a typed exception or returns early with `response.GetErrorResponse(...)`:
```csharp
ResponseDto<EmptyResponseDto> response = ValidateUser(request);
if (response.Status is ResponseStatus.Error)
    return response;
```
