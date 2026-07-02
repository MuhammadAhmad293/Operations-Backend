using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Operations.Dto.DTOs.Auth;
using Operations.IServices.IService;
using System.Security.Claims;

namespace Operations.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private IAuthService AuthService { get; }

        public AuthController(IAuthService authService)
        {
            AuthService = authService;
        }

        [HttpPost("register")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register(RegisterDto dto)
            => Ok(await AuthService.Register(dto, HttpContext.RequestAborted));

        [HttpPost("login")]
        [EnableRateLimiting("auth-login")]
        public async Task<IActionResult> Login(LoginDto dto)
            => Ok(await AuthService.Login(dto, HttpContext.RequestAborted));

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
            => Ok(await AuthService.ChangePassword(User.FindFirstValue(ClaimTypes.NameIdentifier), dto, HttpContext.RequestAborted));

        [HttpPost("forgot-password")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
            => Ok(await AuthService.ForgotPassword(dto, HttpContext.RequestAborted));

        [HttpPost("reset-password")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
            => Ok(await AuthService.ResetPassword(dto, HttpContext.RequestAborted));
    }
}
