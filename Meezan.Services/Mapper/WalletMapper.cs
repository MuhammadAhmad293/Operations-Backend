using Meezan.DataModel.Entities;
using Meezan.Dto.DTOs.Wallet;
using Mapster;

namespace Meezan.Services.Mapper
{
    public class WalletMapper : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<Wallet, WalletDto>();
        }
    }
}
