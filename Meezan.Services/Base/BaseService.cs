using Meezan.IRepositories.UnitOfWork;
using Meezan.Services.Localization;
using MapsterMapper;

namespace Meezan.Services.Base
{
    public class BaseService
    {
        protected IUnitOfWork UnitOfWork { get; }
        protected IMapper Mapper { get; }
        public ILocalizationService Localization { get; }
        public BaseService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService)
        {
            UnitOfWork = unitOfWork;
            Mapper = mapper;
            Localization = localizationService;
        }
    }
}
