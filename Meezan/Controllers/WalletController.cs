using Common.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Meezan.Dto.DTOs.Wallet;
using Meezan.IServices.IService;
using System.Security.Claims;

namespace Meezan.Controllers
{
    [Route("api/wallets")]
    [ApiController]
    [Authorize(AuthenticationSchemes = "Bearer")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public class WalletController : ControllerBase
    {
        private IWalletService WalletService { get; }

        public WalletController(IWalletService walletService)
        {
            WalletService = walletService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseDto<List<WalletDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()
            => Ok(await WalletService.GetAll(User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.RequestAborted));

        [HttpPost]
        [ProducesResponseType(typeof(ResponseDto<EmptyResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Add(CreateWalletDto dto)
            => Ok(await WalletService.Add(User.FindFirstValue(ClaimTypes.NameIdentifier), dto, HttpContext.RequestAborted));

        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(ResponseDto<EmptyResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Update(int id, UpdateWalletDto dto)
        {
            dto.Id = id;
            return Ok(await WalletService.Update(User.FindFirstValue(ClaimTypes.NameIdentifier), dto, HttpContext.RequestAborted));
        }

        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(ResponseDto<EmptyResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Delete(int id)
            => Ok(await WalletService.Delete(User.FindFirstValue(ClaimTypes.NameIdentifier), id, HttpContext.RequestAborted));

        [HttpPost("{id:int}/archive")]
        [ProducesResponseType(typeof(ResponseDto<EmptyResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Archive(int id)
            => Ok(await WalletService.Archive(User.FindFirstValue(ClaimTypes.NameIdentifier), id, HttpContext.RequestAborted));
    }
}
