using Operations.IRepositories.IRepository;
using Operations.IRepositories.UnitOfWork;
using Operations.Repositories.Context;
using Operations.Repositories.Repository;

namespace Operations.Repositories.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork
    {
        public Lazy<AppDbContext> AppDbContext { get; }
        public UnitOfWork(Lazy<AppDbContext> appDbContext) => AppDbContext = appDbContext;

        #region Main Methods Implementation
        public Task<int> CommitAsync(CancellationToken cancellationToken = default) => AppDbContext.Value.SaveChangesAsync(cancellationToken);

        public void Dispose()
        {

        }
        #endregion

        #region Repository Implementation
        public IUserRepository UserRepository => new UserRepository(AppDbContext);
        public IMailRepository MailRepository => new MailRepository(AppDbContext);
        public IPasswordResetTokenRepository PasswordResetTokenRepository => new PasswordResetTokenRepository(AppDbContext);
        #endregion

    }
}
