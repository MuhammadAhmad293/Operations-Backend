using Meezan.DataModel.Entities;
using Meezan.Dto.DTOs;
using Mapster;

namespace Meezan.Services.Mapper
{
    public class UserMapper : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<UserDto, User>();
        }
    }
}
