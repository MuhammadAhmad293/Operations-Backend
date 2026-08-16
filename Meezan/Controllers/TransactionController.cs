using Common.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Meezan.Dto.DTOs.Transaction;
using Meezan.IServices.IService;
using System.Security.Claims;

namespace Meezan.Controllers
{
    [Route("api/transactions")]
    [ApiController]
    [Authorize(AuthenticationSchemes = "Bearer")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public class TransactionController : ControllerBase
    {
        private ITransactionService TransactionService { get; }

        public TransactionController(ITransactionService transactionService)
        {
            TransactionService = transactionService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseDto<List<TransactionGroupDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetFiltered([FromQuery] string? period, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int? walletId, [FromQuery] int? categoryId, [FromQuery] string? type)
            => Ok(await TransactionService.GetFiltered(User.FindFirstValue(ClaimTypes.NameIdentifier), period, from, to, walletId, categoryId, type, HttpContext.RequestAborted));

        [HttpGet("search")]
        [ProducesResponseType(typeof(ResponseDto<List<TransactionGroupDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Search([FromQuery] string q)
            => Ok(await TransactionService.Search(User.FindFirstValue(ClaimTypes.NameIdentifier), q, HttpContext.RequestAborted));

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ResponseDto<TransactionDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetById(int id)
            => Ok(await TransactionService.GetById(User.FindFirstValue(ClaimTypes.NameIdentifier), id, HttpContext.RequestAborted));

        [HttpPost]
        [ProducesResponseType(typeof(ResponseDto<EmptyResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Add(CreateTransactionDto dto)
            => Ok(await TransactionService.Add(User.FindFirstValue(ClaimTypes.NameIdentifier), dto, HttpContext.RequestAborted));

        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(ResponseDto<EmptyResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Update(int id, UpdateTransactionDto dto)
        {
            dto.Id = id;
            return Ok(await TransactionService.Update(User.FindFirstValue(ClaimTypes.NameIdentifier), dto, HttpContext.RequestAborted));
        }

        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(ResponseDto<EmptyResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Delete(int id)
            => Ok(await TransactionService.Delete(User.FindFirstValue(ClaimTypes.NameIdentifier), id, HttpContext.RequestAborted));
    }
}
