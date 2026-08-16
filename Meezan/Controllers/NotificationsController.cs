using Common.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Meezan.Dto.DTOs.Notification;
using Meezan.IServices.IService;
using System.Security.Claims;

namespace Meezan.Controllers
{
    [Route("api/notifications")]
    [ApiController]
    [Authorize(AuthenticationSchemes = "Bearer")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status503ServiceUnavailable)]
    public class NotificationsController : ControllerBase
    {
        private INotificationService NotificationService { get; }

        public NotificationsController(INotificationService notificationService)
        {
            NotificationService = notificationService;
        }

        [HttpGet("login")]
        [ProducesResponseType(typeof(ResponseDto<List<LoginNotificationDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLogin()
            => Ok(await NotificationService.GetLoginNotifications(User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.RequestAborted));
    }
}
