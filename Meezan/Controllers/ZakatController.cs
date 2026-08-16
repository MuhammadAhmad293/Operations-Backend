using Common.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Meezan.Dto.DTOs.Zakat;
using Meezan.IServices.IService;
using System.Security.Claims;

namespace Meezan.Controllers
{
    [Route("api/zakat")]
    [ApiController]
    [Authorize(AuthenticationSchemes = "Bearer")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status503ServiceUnavailable)]
    public class ZakatController : ControllerBase
    {
        private IZakatService ZakatService { get; }

        public ZakatController(IZakatService zakatService)
        {
            ZakatService = zakatService;
        }

        [HttpGet("status")]
        [ProducesResponseType(typeof(ResponseDto<ZakatStatusDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetStatus()
            => Ok(await ZakatService.GetStatus(User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.RequestAborted));

        [HttpGet("cycles")]
        [ProducesResponseType(typeof(ResponseDto<List<ZakatCycleDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCycles()
            => Ok(await ZakatService.GetCycles(User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.RequestAborted));

        [HttpPost("pay")]
        [ProducesResponseType(typeof(ResponseDto<EmptyResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Pay(PayZakatDto dto)
            => Ok(await ZakatService.Pay(User.FindFirstValue(ClaimTypes.NameIdentifier), dto, HttpContext.RequestAborted));

        [HttpPost("cycles/{id:int}/external-payment")]
        [ProducesResponseType(typeof(ResponseDto<EmptyResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> PayExternal(int id, PayExternalZakatDto dto)
            => Ok(await ZakatService.PayExternal(User.FindFirstValue(ClaimTypes.NameIdentifier), id, dto, HttpContext.RequestAborted));
    }
}
