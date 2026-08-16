using Meezan.DataModel.Entities;
using Meezan.Dto.DTOs.Account;
using Mapster;

namespace Meezan.Services.Mapper
{
    public class AccountMapper : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<Account, AccountDto>();
        }
    }
}
