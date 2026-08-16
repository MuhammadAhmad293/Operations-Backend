using Common.Dto;
using Meezan.Dto.DTOs.Statistics;

namespace Meezan.IServices.IService
{
    public interface IStatisticsService
    {
        Task<ResponseDto<StatisticsDto>> GetStatistics(string? userId, string? period, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);

        Task<ResponseDto<StructureDto>> GetStructure(string? userId, string kind, string? period, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
    }
}
