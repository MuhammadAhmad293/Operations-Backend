# Style Guide: service-interfaces

## Unique Conventions

### ResponseDto on Every Method

Every method in a service interface returns `Task<ResponseDto<T>>`. The payload type follows a strict convention:

- Write Meezan → `Task<ResponseDto<EmptyResponseDto>>`
- Single entity reads → `Task<ResponseDto<TDto>>`
- List reads → `Task<ResponseDto<List<TDto>>>`

```csharp
public interface IUserService
{
    Task<ResponseDto<EmptyResponseDto>> Add(UserDto userDto);
    Task<ResponseDto<EmptyResponseDto>> Update(UserDto userDto);
    Task<ResponseDto<EmptyResponseDto>> Delete(int id);
    Task<ResponseDto<UserDto>> GetById(int id);
    Task<ResponseDto<List<UserDto>>> GetAll();
}
```

### Naming Convention

Interface methods match HTTP verb semantics: `Add`, `Update`, `Delete`, `GetById`, `GetAll`.

### Location

Service interfaces live in `Meezan.IServices/IService/` for domain services and `Meezan.IServices/IJob/` for background job services.
