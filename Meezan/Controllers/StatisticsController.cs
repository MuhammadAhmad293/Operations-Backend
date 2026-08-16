using Common.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Meezan.Dto.DTOs.Statistics;
using Meezan.IServices.IService;
using System.Security.Claims;

namespace Meezan.Controllers
{
    [Route("api/statistics")]
    [ApiController]
    [Authorize(AuthenticationSchemes = "Bearer")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status503ServiceUnavailable)]
    public class StatisticsController : ControllerBase
    {
        private IStatisticsService StatisticsService { get; }

        public StatisticsController(IStatisticsService statisticsService)
        {
            StatisticsService = statisticsService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseDto<StatisticsDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetStatistics([FromQuery] string? period, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
            => Ok(await StatisticsService.GetStatistics(User.FindFirstValue(ClaimTypes.NameIdentifier), period, from, to, HttpContext.RequestAborted));

        [HttpGet("structure")]
        [ProducesResponseType(typeof(ResponseDto<StructureDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetStructure([FromQuery] string kind, [FromQuery] string? period, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
            => Ok(await StatisticsService.GetStructure(User.FindFirstValue(ClaimTypes.NameIdentifier), kind, period, from, to, HttpContext.RequestAborted));
    }
}
