using Meezan.DataModel.Entities;
using Meezan.Dto.DTOs.Lookup;
using Mapster;

namespace Meezan.Services.Mapper
{
    public class LookupMapper : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<Currency, CurrencyDto>();
            config.NewConfig<WalletType, WalletTypeDto>();
        }
    }
}
