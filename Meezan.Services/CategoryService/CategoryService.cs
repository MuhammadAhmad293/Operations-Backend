using Common.Dto;
using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;
using Meezan.Dto.DTOs.Category;
using Meezan.IRepositories.UnitOfWork;
using Meezan.IServices.IService;
using Meezan.Services.Base;
using Meezan.Services.CustomExceptions;
using Meezan.Services.Localization;
using MapsterMapper;

namespace Meezan.Services.CategoryService
{
    public class CategoryService : BaseService, ICategoryService
    {
        public CategoryService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localization)
            : base(unitOfWork, mapper, localization)
        {
        }

        #region Public Methods

        public async Task<ResponseDto<List<CategoryDto>>> GetTree(string? userId, string kind, CancellationToken cancellationToken = default)
        {
            ResponseDto<List<CategoryDto>> response = new ResponseDto<List<CategoryDto>>().GetErrorResponse();

            Account account = await GetAccountByUserIdAsync(userId);

            if (!Enum.TryParse(kind, true, out CategoryKind categoryKind))
                throw new InvalidRequestException(Localization.InvalidRequest);

            List<Category> flat = await UnitOfWork.CategoryRepository.GetTreeByKindAsync(account.Id, categoryKind);

            // Children never store their own Color/Icon (BR-06 §2) — resolve from the parent here,
            // at read time, rather than duplicating the parent's values into storage.
            List<CategoryDto> tree = flat
                .Where(c => c.ParentId == null)
                .Select(top =>
                {
                    CategoryDto dto = Mapper.Map<CategoryDto>(top);
                    dto.Children = flat
                        .Where(c => c.ParentId == top.Id)
                        .Select(child =>
                        {
                            CategoryDto childDto = Mapper.Map<CategoryDto>(child);
                            childDto.Color = top.Color;
                            childDto.Icon = top.Icon;
                            return childDto;
                        })
                        .ToList();
                    return dto;
                })
                .ToList();

            return response.GetSuccessResponse(tree);
        }

        public async Task<ResponseDto<EmptyResponseDto>> Add(string? userId, CreateCategoryDto request, CancellationToken cancellationToken = default)
        {
            ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();

            Account account = await GetAccountByUserIdAsync(userId);

            if (request is null || string.IsNullOrWhiteSpace(request.Name))
                throw new InvalidRequestException(Localization.InvalidRequest);

            Category category;

            if (request.ParentId.HasValue)
            {
                Category parent = await UnitOfWork.CategoryRepository.FirstOrDefaultAsync(c => c.Id == request.ParentId.Value && c.AccountId == account.Id && !c.IsDeleted)
                    ?? throw new ObjectNotFoundException(Localization.CategoryNotFound);

                // Max-depth-1 guard: a subcategory's parent must itself be top-level.
                if (parent.ParentId != null)
                    throw new InvalidRequestException(Localization.SubcategoryParentMustBeTopLevel);

                category = new Category
                {
                    Account = account,
                    ParentId = parent.Id,
                    Kind = parent.Kind,
                    Name = request.Name,
                    Color = null,
                    Icon = null,
                    IsProtected = false,
                };
            }
            else
            {
                if (string.IsNullOrWhiteSpace(request.Kind) || !Enum.TryParse(request.Kind, true, out CategoryKind kind))
                    throw new InvalidRequestException(Localization.InvalidRequest);

                category = new Category
                {
                    Account = account,
                    ParentId = null,
                    Kind = kind,
                    Name = request.Name,
                    Color = request.Color,
                    Icon = request.Icon,
                    IsProtected = false,
                };
            }

            UnitOfWork.CategoryRepository.Create(category);

            return await UnitOfWork.CommitAsync(cancellationToken) > default(int)
                ? response.GetSuccessResponse(Localization.GeneralSuccess)
                : response.GetErrorResponse(Localization.GeneralError);
        }

        public async Task<ResponseDto<EmptyResponseDto>> Update(string? userId, UpdateCategoryDto request, CancellationToken cancellationToken = default)
        {
            ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();

            Account account = await GetAccountByUserIdAsync(userId);

            if (request is null || string.IsNullOrWhiteSpace(request.Name))
                throw new InvalidRequestException(Localization.InvalidRequest);

            Category category = await UnitOfWork.CategoryRepository.FirstOrDefaultAsync(c => c.Id == request.Id && c.AccountId == account.Id && !c.IsDeleted)
                ?? throw new ObjectNotFoundException(Localization.CategoryNotFound);

            if (category.IsProtected)
                throw new UnprocessableEntityException(Localization.ProtectedCategoryCannotBeModified);

            category.Name = request.Name;

            // Subcategories always inherit color/icon from their parent at read time — only a
            // top-level category's own Color/Icon are ever stored.
            if (category.ParentId == null)
            {
                category.Color = request.Color;
                category.Icon = request.Icon;
            }

            UnitOfWork.CategoryRepository.Update(category);

            return await UnitOfWork.CommitAsync(cancellationToken) > default(int)
                ? response.GetSuccessResponse(Localization.GeneralSuccess)
                : response.GetErrorResponse(Localization.GeneralError);
        }

        public async Task<ResponseDto<EmptyResponseDto>> Delete(string? userId, int id, CancellationToken cancellationToken = default)
        {
            ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();

            Account account = await GetAccountByUserIdAsync(userId);

            Category category = await UnitOfWork.CategoryRepository.FirstOrDefaultAsync(c => c.Id == id && c.AccountId == account.Id && !c.IsDeleted)
                ?? throw new ObjectNotFoundException(Localization.CategoryNotFound);

            if (category.IsProtected)
                throw new UnprocessableEntityException(Localization.ProtectedCategoryCannotBeModified);

            // A category is "referenced" (soft-delete, BR-06) either because a transaction points
            // at it, or because it still has live children — hard-deleting a parent with children
            // would violate the self-referencing FK (Restrict, per the plan's FK decisions).
            bool isReferenced = await UnitOfWork.TransactionRepository.ExistsForCategoryAsync(category.Id)
                || await UnitOfWork.CategoryRepository.HasChildrenAsync(category.Id);

            if (isReferenced)
            {
                category.IsDeleted = true;
                UnitOfWork.CategoryRepository.Update(category);
            }
            else
            {
                UnitOfWork.CategoryRepository.Delete(category);
            }

            return await UnitOfWork.CommitAsync(cancellationToken) > default(int)
                ? response.GetSuccessResponse(Localization.CategoryDeleted)
                : response.GetErrorResponse(Localization.GeneralError);
        }

        #endregion
    }
}
