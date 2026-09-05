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

        // Internal-only (Phase 017) — no public HTTP endpoint calls this directly (WalletService
        // calls it from AdjustBalance). Reuses the same private ValidateAndResolveAsync helper
        // Add/Update share, so every existing transaction rule (dual-calendar dates, karat
        // computation, category-kind matching, Zakat re-evaluation) applies identically; the only
        // difference is IsAdjustment=true. CreateTransactionDto has no IsAdjustment field, so an
        // ordinary POST /api/transactions request can never set this flag itself.
        Task<ResponseDto<EmptyResponseDto>> AddAdjustment(string? userId, CreateTransactionDto dto, CancellationToken cancellationToken = default);
    }
}
