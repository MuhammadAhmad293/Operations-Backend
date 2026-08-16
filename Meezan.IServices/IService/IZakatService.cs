using Common.Dto;
using Meezan.Dto.DTOs.Zakat;

namespace Meezan.IServices.IService
{
    public interface IZakatService
    {
        Task<ResponseDto<ZakatStatusDto>> GetStatus(string? userId, CancellationToken cancellationToken = default);

        Task<ResponseDto<List<ZakatCycleDto>>> GetCycles(string? userId, CancellationToken cancellationToken = default);

        Task<ResponseDto<EmptyResponseDto>> Pay(string? userId, PayZakatDto request, CancellationToken cancellationToken = default);

        Task<ResponseDto<EmptyResponseDto>> PayExternal(string? userId, int cycleId, PayExternalZakatDto request, CancellationToken cancellationToken = default);
    }
}
