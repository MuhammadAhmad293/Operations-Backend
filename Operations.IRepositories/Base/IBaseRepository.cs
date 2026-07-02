using System.Linq.Expressions;

namespace Operations.IRepositories.IRepository
{
    public interface IBaseRepository<T>
    {
        void Create(T entity);
        void Update(T entity);
        void Delete(T entity);
        Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> filter);
        Task<List<T>> GetAllAsync();
    }
}
