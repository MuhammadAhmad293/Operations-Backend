using Common.Dto;
using Meezan.Dto.DTOs.Calendar;

namespace Meezan.IServices.IService
{
    public interface ICalendarService
    {
        Task<ResponseDto<CalendarDto>> GetMonth(string? userId, int year, int month, CancellationToken cancellationToken = default);
    }
}
