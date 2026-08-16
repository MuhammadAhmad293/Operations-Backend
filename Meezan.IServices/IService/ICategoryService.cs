using Common.Dto;
using Meezan.Dto.DTOs.Category;

namespace Meezan.IServices.IService
{
    public interface ICategoryService
    {
        Task<ResponseDto<List<CategoryDto>>> GetTree(string? userId, string kind, CancellationToken cancellationToken = default);
        Task<ResponseDto<EmptyResponseDto>> Add(string? userId, CreateCategoryDto dto, CancellationToken cancellationToken = default);
        Task<ResponseDto<EmptyResponseDto>> Update(string? userId, UpdateCategoryDto dto, CancellationToken cancellationToken = default);
        Task<ResponseDto<EmptyResponseDto>> Delete(string? userId, int id, CancellationToken cancellationToken = default);
    }
}
