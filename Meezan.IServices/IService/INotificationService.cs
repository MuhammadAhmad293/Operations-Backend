using Common.Dto;
using Meezan.Dto.DTOs.Notification;

namespace Meezan.IServices.IService
{
    public interface INotificationService
    {
        Task<ResponseDto<List<LoginNotificationDto>>> GetLoginNotifications(string? userId, CancellationToken cancellationToken = default);
    }
}
