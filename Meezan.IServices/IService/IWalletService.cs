using Common.Dto;
using Meezan.Dto.DTOs.Wallet;

namespace Meezan.IServices.IService
{
    public interface IWalletService
    {
        Task<ResponseDto<List<WalletDto>>> GetAll(string? userId, CancellationToken cancellationToken = default);
        Task<ResponseDto<EmptyResponseDto>> Add(string? userId, CreateWalletDto dto, CancellationToken cancellationToken = default);
        Task<ResponseDto<EmptyResponseDto>> Update(string? userId, UpdateWalletDto dto, CancellationToken cancellationToken = default);
        Task<ResponseDto<EmptyResponseDto>> Archive(string? userId, int id, CancellationToken cancellationToken = default);
        Task<ResponseDto<EmptyResponseDto>> Delete(string? userId, int id, CancellationToken cancellationToken = default);
        Task<ResponseDto<EmptyResponseDto>> AdjustBalance(string? userId, AdjustWalletBalanceDto dto, CancellationToken cancellationToken = default);
        Task<ResponseDto<EmptyResponseDto>> SetInitialAmount(string? userId, SetWalletInitialAmountDto dto, CancellationToken cancellationToken = default);
    }
}
