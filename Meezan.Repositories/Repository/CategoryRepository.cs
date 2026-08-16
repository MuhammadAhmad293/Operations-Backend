using Microsoft.EntityFrameworkCore;
using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;
using Meezan.IRepositories.IRepository;
using Meezan.Repositories.Base;
using Meezan.Repositories.Context;

namespace Meezan.Repositories.Repository
{
    public class CategoryRepository : BaseRepository<Category>, ICategoryRepository
    {
        public CategoryRepository(Lazy<AppDbContext> appDbContext) : base(appDbContext)
        {
        }

        // Flat list (both top-level and children) — the service builds the tree structure and
        // resolves child color/icon inheritance from it.
        public async Task<List<Category>> GetTreeByKindAsync(int accountId, CategoryKind kind)
            => await AppDbContext.Value.Set<Category>()
                .Where(c => c.AccountId == accountId && c.Kind == kind && !c.IsDeleted)
                .OrderBy(c => c.SortOrder)
                .ToListAsync();

        public async Task<bool> HasChildrenAsync(int categoryId)
            => await AppDbContext.Value.Set<Category>()
                .AnyAsync(c => c.ParentId == categoryId && !c.IsDeleted);

        public async Task<List<Category>> GetByKindIncludingDeletedAsync(int accountId, CategoryKind kind)
            => await AppDbContext.Value.Set<Category>()
                .Where(c => c.AccountId == accountId && c.Kind == kind)
                .ToListAsync();
    }
}
