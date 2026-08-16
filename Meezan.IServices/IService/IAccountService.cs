using Common.Dto;
using Meezan.Dto.DTOs.Account;

namespace Meezan.IServices.IService
{
    public interface IAccountService
    {
        Task<ResponseDto<AccountDto>> GetByUser(string? userId, CancellationToken cancellationToken = default);
        Task<ResponseDto<EmptyResponseDto>> Create(string? userId, CreateAccountDto dto, CancellationToken cancellationToken = default);
        Task<ResponseDto<EmptyResponseDto>> UpdateSettings(string? userId, AccountSettingsDto dto, CancellationToken cancellationToken = default);
    }
}
