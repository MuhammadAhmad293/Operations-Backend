using Common.Dto;
using Meezan.Dto.DTOs.Lookup;

namespace Meezan.IServices.IService
{
    public interface ILookupService
    {
        Task<ResponseDto<List<CurrencyDto>>> GetCurrencies(CancellationToken cancellationToken = default);
        Task<ResponseDto<List<WalletTypeDto>>> GetWalletTypes(CancellationToken cancellationToken = default);
    }
}
