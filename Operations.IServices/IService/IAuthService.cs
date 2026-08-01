using Common.Dto;
using Operations.Dto.DTOs.Auth;

namespace Operations.IServices.IService
{
    public interface IAuthService
    {
        Task<ResponseDto<EmptyResponseDto>> Register(RegisterDto dto, CancellationToken cancellationToken = default);
        Task<ResponseDto<LoginResponseDto>> Login(LoginDto dto, string? ipAddress, string? deviceId, string? deviceName, string? platform, CancellationToken cancellationToken = default);
        Task<ResponseDto<EmptyResponseDto>> ChangePassword(string? userId, ChangePasswordDto dto, CancellationToken cancellationToken = default);
        Task<ResponseDto<EmptyResponseDto>> ForgotPassword(ForgotPasswordDto dto, CancellationToken cancellationToken = default);
        Task<ResponseDto<EmptyResponseDto>> ResetPassword(ResetPasswordDto dto, CancellationToken cancellationToken = default);
        Task<ResponseDto<LoginResponseDto>> RefreshToken(string? refreshToken, string? ipAddress, string? deviceId, string? deviceName, string? platform, CancellationToken cancellationToken = default);
        Task<ResponseDto<EmptyResponseDto>> Logout(string? refreshToken, CancellationToken cancellationToken = default);
        Task<ResponseDto<EmptyResponseDto>> LogoutAllDevices(string? userId, CancellationToken cancellationToken = default);
        Task<ResponseDto<List<SessionDto>>> GetActiveSessions(string? userId, CancellationToken cancellationToken = default);
        Task<ResponseDto<EmptyResponseDto>> RevokeSession(string? userId, int refreshTokenId, CancellationToken cancellationToken = default);
    }
}
