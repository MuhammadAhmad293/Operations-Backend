using Common.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Meezan.Dto.DTOs.Overview;
using Meezan.IServices.IService;
using System.Security.Claims;

namespace Meezan.Controllers
{
    [Route("api/overview")]
    [ApiController]
    [Authorize(AuthenticationSchemes = "Bearer")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status503ServiceUnavailable)]
    public class OverviewController : ControllerBase
    {
        private IOverviewService OverviewService { get; }

        public OverviewController(IOverviewService overviewService)
        {
            OverviewService = overviewService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseDto<OverviewDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetOverview([FromQuery] string? period, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
            => Ok(await OverviewService.GetOverview(User.FindFirstValue(ClaimTypes.NameIdentifier), period, from, to, HttpContext.RequestAborted));
    }
}
