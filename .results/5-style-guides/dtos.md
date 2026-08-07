# Style Guide: dtos

## Unique Conventions

### ResponseDto Wrapper

All service method return values are wrapped in `ResponseDto<T>`. The generic parameter `T` is the payload type:

- `ResponseDto<EmptyResponseDto>` — for write Meezan (create/update/delete) where no data is returned.
- `ResponseDto<UserDto>` — for single-entity reads.
- `ResponseDto<List<UserDto>>` — for list reads.

`ResponseDto<T>` is always initialised in Error state and then mutated via fluent methods:

```csharp
ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();
return response.GetSuccessResponse(Localization.GeneralSuccess);
```

### EmptyResponseDto as Write Response Payload

Write Meezan use `EmptyResponseDto` as the `T` in `ResponseDto<T>` to indicate no data payload:

```csharp
Task<ResponseDto<EmptyResponseDto>> Add(UserDto userDto);
```

### DTO Property Style

DTOs are plain POCO classes with auto-properties, no validation attributes, no constructors, and no fluent builders:

```csharp
public class UserDto
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    // ...
}
```

### No Inheritance Between DTOs

DTOs do not inherit from each other. `ResponseDto<T>` is the only generic wrapper.

### Pagination DTOs

`PaginationRequestDto` and `PaginationResponseDto` are defined in `Common` for reuse across future list endpoints.

### Mail DTOs

Mail-related DTOs (`MailDto`, `MailSettingDto`) live in `Common/Notification/Mail/` because they are part of the cross-cutting notification concern, not domain DTOs.
