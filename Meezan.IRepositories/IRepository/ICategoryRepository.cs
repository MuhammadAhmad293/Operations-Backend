using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;

namespace Meezan.IRepositories.IRepository
{
    public interface ICategoryRepository : IBaseRepository<Category>
    {
        Task<List<Category>> GetTreeByKindAsync(int accountId, CategoryKind kind);
        Task<bool> HasChildrenAsync(int categoryId);

        // Includes soft-deleted categories — BR-06: a soft-deleted category's historical
        // transactions must still resolve its name (same reasoning as
        // IWalletRepository.GetByAccountIncludingDeletedAsync), needed for the Structure/donut
        // view (Phase 011) to label every category a period's transactions could reference.
        Task<List<Category>> GetByKindIncludingDeletedAsync(int accountId, CategoryKind kind);
    }
}
