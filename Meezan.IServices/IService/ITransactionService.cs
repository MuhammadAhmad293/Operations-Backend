using Common.Dto;
using Meezan.Dto.DTOs.Transaction;

namespace Meezan.IServices.IService
{
    public interface ITransactionService
    {
        Task<ResponseDto<List<TransactionGroupDto>>> GetFiltered(string? userId, string? period, DateOnly? from, DateOnly? to, int? walletId, int? categoryId, string? type, CancellationToken cancellationToken = default);
        Task<ResponseDto<List<TransactionGroupDto>>> Search(string? userId, string query, CancellationToken cancellationToken = default);
        Task<ResponseDto<TransactionDto>> GetById(string? userId, int id, CancellationToken cancellationToken = default);
        Task<ResponseDto<EmptyResponseDto>> Add(string? userId, CreateTransactionDto dto, CancellationToken cancellationToken = default);
        Task<ResponseDto<EmptyResponseDto>> Update(string? userId, UpdateTransactionDto dto, CancellationToken cancellationToken = default);
        Task<ResponseDto<EmptyResponseDto>> Delete(string? userId, int id, CancellationToken cancellationToken = default);
    }
}
