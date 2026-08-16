using Common.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Meezan.Dto.DTOs.Lookup;
using Meezan.IServices.IService;

namespace Meezan.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = "Bearer")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public class LookupsController : ControllerBase
    {
        private ILookupService LookupService { get; }

        public LookupsController(ILookupService lookupService)
        {
            LookupService = lookupService;
        }

        [HttpGet("currencies")]
        [ProducesResponseType(typeof(ResponseDto<List<CurrencyDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCurrencies()
            => Ok(await LookupService.GetCurrencies(HttpContext.RequestAborted));

        [HttpGet("wallet-types")]
        [ProducesResponseType(typeof(ResponseDto<List<WalletTypeDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetWalletTypes()
            => Ok(await LookupService.GetWalletTypes(HttpContext.RequestAborted));
    }
}
