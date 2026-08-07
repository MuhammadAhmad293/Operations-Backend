using Meezan.IRepositories.IRepository;
using Meezan.Repositories.Context;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Meezan.Repositories.Base
{
    public abstract class BaseRepository<T> : IBaseRepository<T> where T : class
    {
        public Lazy<AppDbContext> AppDbContext { get; }
        internal BaseRepository(Lazy<AppDbContext> appDbContext)
        {
            AppDbContext = appDbContext;
        }
        public void Create(T entity) => AppDbContext.Value.Set<T>().Add(entity);

        public void Update(T entity) => AppDbContext.Value.Set<T>().Update(entity);

        public void Delete(T entity) => AppDbContext.Value.Set<T>().Remove(entity);

        public async Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> filter)
        {
            IQueryable<T> query = AppDbContext.Value.Set<T>();

            if (filter != null)
            {
                query = query.Where(filter);
            }
            return await query.FirstOrDefaultAsync();
        }
        public async Task<bool> AnyAsync(Expression<Func<T, bool>> filter)
        {
            IQueryable<T> query = AppDbContext.Value.Set<T>();

            if (filter != null)
            {
                return await query.AnyAsync(filter);
            }
            return await query.AnyAsync();
        }
        public async Task<List<T>> GetAllAsync() => await AppDbContext.Value.Set<T>().ToListAsync();

    }
}
