using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Common.Dto;
using Meezan.Dto.DTOs.Attachment;
using Meezan.IServices.IService;
using Meezan.Services.CustomExceptions;
using System.Security.Claims;

namespace Meezan.Controllers
{
    [Route("api/attachments")]
    [ApiController]
    [Authorize(AuthenticationSchemes = "Bearer")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public class AttachmentController : ControllerBase
    {
        private IAttachmentService AttachmentService { get; }

        public AttachmentController(IAttachmentService attachmentService)
        {
            AttachmentService = attachmentService;
        }

        [HttpPost("/api/transactions/{transactionId:int}/attachments")]
        [ProducesResponseType(typeof(ResponseDto<AttachmentDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Upload(int transactionId, IFormFile file)
        {
            if (file is null || file.Length == 0)
                throw new InvalidRequestException();

            await using Stream stream = file.OpenReadStream();
            return Ok(await AttachmentService.Upload(User.FindFirstValue(ClaimTypes.NameIdentifier), transactionId, stream, file.FileName, file.ContentType, file.Length, HttpContext.RequestAborted));
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
        public async Task<IActionResult> Download(int id)
        {
            ResponseDto<AttachmentContentDto> response = await AttachmentService.Download(User.FindFirstValue(ClaimTypes.NameIdentifier), id, HttpContext.RequestAborted);
            return File(response.Data.Content, response.Data.MimeType, response.Data.FileName);
        }

        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(ResponseDto<EmptyResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Delete(int id)
            => Ok(await AttachmentService.Delete(User.FindFirstValue(ClaimTypes.NameIdentifier), id, HttpContext.RequestAborted));
    }
}
