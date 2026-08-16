using Common.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Meezan.Dto.DTOs.Calendar;
using Meezan.IServices.IService;
using System.Security.Claims;

namespace Meezan.Controllers
{
    [Route("api/calendar")]
    [ApiController]
    [Authorize(AuthenticationSchemes = "Bearer")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status503ServiceUnavailable)]
    public class CalendarController : ControllerBase
    {
        private ICalendarService CalendarService { get; }

        public CalendarController(ICalendarService calendarService)
        {
            CalendarService = calendarService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseDto<CalendarDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMonth([FromQuery] int year, [FromQuery] int month)
            => Ok(await CalendarService.GetMonth(User.FindFirstValue(ClaimTypes.NameIdentifier), year, month, HttpContext.RequestAborted));
    }
}
