using Meezan.DataModel.Entities;
using Meezan.Dto.DTOs.Category;
using Mapster;

namespace Meezan.Services.Mapper
{
    public class CategoryMapper : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<Category, CategoryDto>();
        }
    }
}
