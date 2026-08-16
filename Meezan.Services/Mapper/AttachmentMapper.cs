using Meezan.DataModel.Entities;
using Meezan.Dto.DTOs.Attachment;
using Mapster;

namespace Meezan.Services.Mapper
{
    public class AttachmentMapper : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<Attachment, AttachmentDto>();
        }
    }
}
