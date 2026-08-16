using Common.Dto;
using Meezan.Dto.DTOs.Attachment;

namespace Meezan.IServices.IService
{
    public interface IAttachmentService
    {
        Task<ResponseDto<AttachmentDto>> Upload(string? userId, int transactionId, Stream content, string fileName, string mimeType, long sizeBytes, CancellationToken cancellationToken = default);
        Task<ResponseDto<AttachmentContentDto>> Download(string? userId, int id, CancellationToken cancellationToken = default);
        Task<ResponseDto<EmptyResponseDto>> Delete(string? userId, int id, CancellationToken cancellationToken = default);
    }
}
