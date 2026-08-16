using Common.Dto;
using Meezan.Dto.DTOs.Overview;

namespace Meezan.IServices.IService
{
    public interface IOverviewService
    {
        Task<ResponseDto<OverviewDto>> GetOverview(string? userId, string? period, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
    }
}
