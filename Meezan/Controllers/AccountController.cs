using Common.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Meezan.Dto.DTOs.Account;
using Meezan.IServices.IService;
using System.Security.Claims;

namespace Meezan.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = "Bearer")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public class AccountController : ControllerBase
    {
        private IAccountService AccountService { get; }

        public AccountController(IAccountService accountService)
        {
            AccountService = accountService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseDto<AccountDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Get()
            => Ok(await AccountService.GetByUser(User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.RequestAborted));

        [HttpPost]
        [ProducesResponseType(typeof(ResponseDto<EmptyResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Create(CreateAccountDto dto)
            => Ok(await AccountService.Create(User.FindFirstValue(ClaimTypes.NameIdentifier), dto, HttpContext.RequestAborted));

        [HttpPut("settings")]
        [ProducesResponseType(typeof(ResponseDto<EmptyResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateSettings(AccountSettingsDto dto)
            => Ok(await AccountService.UpdateSettings(User.FindFirstValue(ClaimTypes.NameIdentifier), dto, HttpContext.RequestAborted));
    }
}
