using Common.Dto;
using Common.FileStorage;
using Meezan.DataModel.Entities;
using Meezan.Dto.DTOs.Attachment;
using Meezan.IRepositories.UnitOfWork;
using Meezan.IServices.IService;
using Meezan.Services.Base;
using Meezan.Services.CustomExceptions;
using Meezan.Services.Localization;
using MapsterMapper;
using Microsoft.Extensions.Logging;

namespace Meezan.Services.AttachmentService
{
    public class AttachmentService : BaseService, IAttachmentService
    {
        private const long MaxSizeBytes = 10 * 1024 * 1024; // spec §10: 10 MB per file

        private static readonly HashSet<string> AllowedExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".webp" };

        private static readonly HashSet<string> AllowedMimeTypes =
            new(StringComparer.OrdinalIgnoreCase) { "application/pdf", "image/jpeg", "image/png", "image/gif", "image/webp" };

        private IFileStorageService FileStorageService { get; }
        private ILogger<AttachmentService> Logger { get; }

        public AttachmentService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localization,
            IFileStorageService fileStorageService, ILogger<AttachmentService> logger)
            : base(unitOfWork, mapper, localization)
        {
            FileStorageService = fileStorageService;
            Logger = logger;
        }

        #region Public Methods

        public async Task<ResponseDto<AttachmentDto>> Upload(string? userId, int transactionId, Stream content, string fileName, string mimeType, long sizeBytes, CancellationToken cancellationToken = default)
        {
            ResponseDto<AttachmentDto> response = new ResponseDto<AttachmentDto>().GetErrorResponse();

            Account account = await GetAccountByUserIdAsync(userId);

            Transaction transaction = await UnitOfWork.TransactionRepository.FirstOrDefaultAsync(t => t.Id == transactionId && t.AccountId == account.Id && !t.IsDeleted)
                ?? throw new ObjectNotFoundException(Localization.TransactionNotFound);

            if (string.IsNullOrWhiteSpace(fileName))
                throw new InvalidRequestException(Localization.InvalidRequest);

            if (sizeBytes <= 0 || sizeBytes > MaxSizeBytes)
                throw new InvalidRequestException(Localization.AttachmentTooLarge);

            string extension = Path.GetExtension(fileName);
            if (!AllowedExtensions.Contains(extension) || !AllowedMimeTypes.Contains(mimeType))
                throw new InvalidRequestException(Localization.InvalidAttachmentType);

            string storagePath = await FileStorageService.SaveAsync(content, fileName, cancellationToken);

            Attachment attachment = new()
            {
                Transaction = transaction,
                FileName = fileName,
                MimeType = mimeType,
                SizeBytes = (int)sizeBytes,
                StoragePath = storagePath,
            };
            UnitOfWork.AttachmentRepository.Create(attachment);

            if (await UnitOfWork.CommitAsync(cancellationToken) <= default(int))
            {
                // The DB row failed to save — don't leave an orphaned file behind.
                await FileStorageService.DeleteAsync(storagePath);
                return response.GetErrorResponse(Localization.GeneralError);
            }

            return response.GetSuccessResponse(Mapper.Map<AttachmentDto>(attachment));
        }

        public async Task<ResponseDto<AttachmentContentDto>> Download(string? userId, int id, CancellationToken cancellationToken = default)
        {
            ResponseDto<AttachmentContentDto> response = new ResponseDto<AttachmentContentDto>().GetErrorResponse();

            Account account = await GetAccountByUserIdAsync(userId);

            Attachment attachment = await UnitOfWork.AttachmentRepository.FirstOrDefaultAsync(a => a.Id == id && a.Transaction.AccountId == account.Id)
                ?? throw new ObjectNotFoundException(Localization.AttachmentNotFound);

            Stream stream = await FileStorageService.OpenReadAsync(attachment.StoragePath);

            return response.GetSuccessResponse(new AttachmentContentDto
            {
                Content = stream,
                FileName = attachment.FileName,
                MimeType = attachment.MimeType,
            });
        }

        public async Task<ResponseDto<EmptyResponseDto>> Delete(string? userId, int id, CancellationToken cancellationToken = default)
        {
            ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();

            Account account = await GetAccountByUserIdAsync(userId);

            Attachment attachment = await UnitOfWork.AttachmentRepository.FirstOrDefaultAsync(a => a.Id == id && a.Transaction.AccountId == account.Id)
                ?? throw new ObjectNotFoundException(Localization.AttachmentNotFound);

            UnitOfWork.AttachmentRepository.Delete(attachment);

            int result = await UnitOfWork.CommitAsync(cancellationToken);

            if (result > default(int))
            {
                try
                {
                    await FileStorageService.DeleteAsync(attachment.StoragePath);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to delete physical file {StoragePath} for attachment {AttachmentId}", attachment.StoragePath, attachment.Id);
                }
            }

            return result > default(int)
                ? response.GetSuccessResponse(Localization.GeneralSuccess)
                : response.GetErrorResponse(Localization.GeneralError);
        }

        #endregion
    }
}
