using Common.Dto;
using Meezan.DataModel.Entities;
using Meezan.Dto.DTOs.Lookup;
using Meezan.IRepositories.UnitOfWork;
using Meezan.IServices.IService;
using Meezan.Services.Base;
using Meezan.Services.Localization;
using MapsterMapper;

namespace Meezan.Services.LookupService
{
    public class LookupService : BaseService, ILookupService
    {
        public LookupService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localization)
            : base(unitOfWork, mapper, localization)
        {
        }

        public async Task<ResponseDto<List<CurrencyDto>>> GetCurrencies(CancellationToken cancellationToken = default)
        {
            ResponseDto<List<CurrencyDto>> response = new ResponseDto<List<CurrencyDto>>().GetErrorResponse();

            List<Currency> currencies = await UnitOfWork.CurrencyRepository.GetAllAsync();

            return response.GetSuccessResponse(Mapper.Map<List<CurrencyDto>>(currencies));
        }

        public async Task<ResponseDto<List<WalletTypeDto>>> GetWalletTypes(CancellationToken cancellationToken = default)
        {
            ResponseDto<List<WalletTypeDto>> response = new ResponseDto<List<WalletTypeDto>>().GetErrorResponse();

            List<WalletType> walletTypes = await UnitOfWork.WalletTypeRepository.GetAllAsync();

            return response.GetSuccessResponse(Mapper.Map<List<WalletTypeDto>>(walletTypes));
        }
    }
}
