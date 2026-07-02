using Common.Dto;
using Operations.Dto.DTOs.Auth;

namespace Operations.IServices.IService
{
    public interface IAuthService
    {
        Task<ResponseDto<EmptyResponseDto>> Register(RegisterDto dto, CancellationToken cancellationToken = default);
        Task<ResponseDto<LoginResponseDto>> Login(LoginDto dto, CancellationToken cancellationToken = default);
        Task<ResponseDto<EmptyResponseDto>> ChangePassword(string? userId, ChangePasswordDto dto, CancellationToken cancellationToken = default);
        Task<ResponseDto<EmptyResponseDto>> ForgotPassword(ForgotPasswordDto dto, CancellationToken cancellationToken = default);
        Task<ResponseDto<EmptyResponseDto>> ResetPassword(ResetPasswordDto dto, CancellationToken cancellationToken = default);
    }
}
