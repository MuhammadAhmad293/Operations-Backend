using Common.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Meezan.Dto.DTOs.Rate;
using Meezan.IServices.IService;

namespace Meezan.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = "Bearer")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public class RatesController : ControllerBase
    {
        private IRateService RateService { get; }

        public RatesController(IRateService rateService)
        {
            RateService = rateService;
        }

        // GET /api/rates/latest?base=SAR&quotes=USD,GOLD — API #24. Never live-calls a
        // provider (BR-19); resolves entirely from the latest stored snapshots.
        [HttpGet("latest")]
        [ProducesResponseType(typeof(ResponseDto<RatesResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GetLatest([FromQuery(Name = "base")] string baseCurrencyCode, [FromQuery] string quotes)
            => Ok(await RateService.GetLatestQuotesAsync(
                baseCurrencyCode,
                quotes?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() ?? new List<string>(),
                HttpContext.RequestAborted));
    }
}
