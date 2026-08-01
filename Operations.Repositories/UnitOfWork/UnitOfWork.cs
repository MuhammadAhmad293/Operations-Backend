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

        public async Task<int> ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
        {
            await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
                await AppDbContext.Value.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await operation(cancellationToken);
                int result = await AppDbContext.Value.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public void Dispose()
        {

        }
        #endregion

        #region Repository Implementation
        public IUserRepository UserRepository => new UserRepository(AppDbContext);
        public IMailRepository MailRepository => new MailRepository(AppDbContext);
        public IPasswordResetTokenRepository PasswordResetTokenRepository => new PasswordResetTokenRepository(AppDbContext);
        public IOutboxRepository OutboxRepository => new OutboxRepository(AppDbContext);
        public IProcessedMessageRepository ProcessedMessageRepository => new ProcessedMessageRepository(AppDbContext);
        public IRefreshTokenRepository RefreshTokenRepository => new RefreshTokenRepository(AppDbContext);
        #endregion

    }
}
