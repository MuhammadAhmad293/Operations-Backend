using Common.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Meezan.Dto.DTOs.Auth;
using Meezan.IServices.IService;
using Meezan.Services.Setting;
using System.Security.Claims;

namespace Meezan.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public class AuthController : ControllerBase
    {
        private const string RefreshTokenCookieName = "refreshToken";

        private IAuthService AuthService { get; }
        private RefreshTokenSettings RefreshTokenSettings { get; }

        public AuthController(IAuthService authService, RefreshTokenSettings refreshTokenSettings)
        {
            AuthService = authService;
            RefreshTokenSettings = refreshTokenSettings;
        }

        [HttpPost("register")]
        [EnableRateLimiting("auth")]
        [AllowAnonymous]
        public async Task<IActionResult> Register(RegisterDto dto)
            => Ok(await AuthService.Register(dto, HttpContext.RequestAborted));

        [HttpPost("login")]
        [EnableRateLimiting("auth-login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            return Ok(await AuthService.Login(dto, ipAddress, dto.DeviceId, dto.DeviceName, dto.Platform, HttpContext.RequestAborted));
        }

        [HttpPost("web/login")]
        [EnableRateLimiting("auth-login")]
        [AllowAnonymous]
        public async Task<IActionResult> WebLogin(LoginDto dto)
        {
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            ResponseDto<LoginResponseDto> result = await AuthService.Login(dto, ipAddress, dto.DeviceId, dto.DeviceName, "web", HttpContext.RequestAborted);
            MoveRefreshTokenToCookie(result);
            return Ok(result);
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
            => Ok(await AuthService.ChangePassword(User.FindFirstValue(ClaimTypes.NameIdentifier), dto, HttpContext.RequestAborted));

        [HttpPost("forgot-password")]
        [EnableRateLimiting("auth")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
            => Ok(await AuthService.ForgotPassword(dto, HttpContext.RequestAborted));

        [HttpPost("reset-password")]
        [EnableRateLimiting("auth")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
            => Ok(await AuthService.ResetPassword(dto, HttpContext.RequestAborted));

        [HttpPost("refresh-token")]
        [EnableRateLimiting("auth-refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken(RefreshTokenRequestDto? dto)
        {
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            return Ok(await AuthService.RefreshToken(dto?.RefreshToken, ipAddress, dto?.DeviceId, dto?.DeviceName, dto?.Platform, HttpContext.RequestAborted));
        }

        [HttpPost("web/refresh-token")]
        [EnableRateLimiting("auth-refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> WebRefreshToken()
        {
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            string presentedToken = Request.Cookies[RefreshTokenCookieName];

            ResponseDto<LoginResponseDto> result = await AuthService.RefreshToken(presentedToken, ipAddress, null, null, "web", HttpContext.RequestAborted);
            MoveRefreshTokenToCookie(result);
            return Ok(result);
        }

        [HttpPost("logout")]
        [EnableRateLimiting("auth")]
        [AllowAnonymous]
        public async Task<IActionResult> Logout(LogoutDto? dto)
            => Ok(await AuthService.Logout(dto?.RefreshToken, HttpContext.RequestAborted));

        [HttpPost("web/logout")]
        [EnableRateLimiting("auth")]
        [AllowAnonymous]
        public async Task<IActionResult> WebLogout()
        {
            string presentedToken = Request.Cookies[RefreshTokenCookieName];
            ResponseDto<EmptyResponseDto> result = await AuthService.Logout(presentedToken, HttpContext.RequestAborted);
            ClearRefreshTokenCookie();
            return Ok(result);
        }

        [HttpPost("logout-all")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> LogoutAllDevices()
            => Ok(await AuthService.LogoutAllDevices(User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.RequestAborted));

        [HttpGet("sessions")]
        public async Task<IActionResult> GetActiveSessions()
            => Ok(await AuthService.GetActiveSessions(User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.RequestAborted));

        [HttpPost("sessions/{id}/revoke")]
        public async Task<IActionResult> RevokeSession(int id)
            => Ok(await AuthService.RevokeSession(User.FindFirstValue(ClaimTypes.NameIdentifier), id, HttpContext.RequestAborted));

        private void MoveRefreshTokenToCookie(ResponseDto<LoginResponseDto> result)
        {
            if (result.Data is null)
                return;

            SetRefreshTokenCookie(result.Data.RefreshToken);
            result.Data.RefreshToken = null;
        }

        private void SetRefreshTokenCookie(string token)
        {
            CookiePolicySettings cookie = RefreshTokenSettings.Cookie;
            Response.Cookies.Append(RefreshTokenCookieName, token, new CookieOptions
            {
                HttpOnly = true,
                Secure = cookie.Secure,
                SameSite = Enum.Parse<SameSiteMode>(cookie.SameSite, ignoreCase: true),
                Path = cookie.Path,
                Domain = string.IsNullOrWhiteSpace(cookie.Domain) ? null : cookie.Domain,
                Expires = DateTimeOffset.UtcNow.AddDays(RefreshTokenSettings.RefreshTokenExpiryDays)
            });
        }

        private void ClearRefreshTokenCookie()
        {
            CookiePolicySettings cookie = RefreshTokenSettings.Cookie;
            Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
            {
                Path = cookie.Path,
                Domain = string.IsNullOrWhiteSpace(cookie.Domain) ? null : cookie.Domain
            });
        }
    }
}
