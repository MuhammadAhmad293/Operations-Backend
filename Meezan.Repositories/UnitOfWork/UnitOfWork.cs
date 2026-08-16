using Meezan.IRepositories.IRepository;
using Meezan.IRepositories.UnitOfWork;
using Meezan.Repositories.Context;
using Meezan.Repositories.Repository;
using Microsoft.EntityFrameworkCore;

namespace Meezan.Repositories.UnitOfWork
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

            // Some delegates (e.g. ZakatEngine.ReevaluateAsync) call UnitOfWork.CommitAsync
            // themselves mid-delegate, to read their own writes before this method's own final
            // SaveChangesAsync runs. That intermediate save's row count would otherwise be lost —
            // the final SaveChangesAsync below only reports what's *still* pending, so a delegate
            // whose only staged change was already flushed by such an intermediate call would
            // make this return 0 despite real work having committed. Summing every SavedChanges
            // event fired on this DbContext for the duration of the delegate reports the true total.
            int totalSaved = 0;
            void OnSavedChanges(object? sender, SavedChangesEventArgs e) => totalSaved += e.EntitiesSavedCount;
            AppDbContext.Value.SavedChanges += OnSavedChanges;
            try
            {
                await operation(cancellationToken);
                await AppDbContext.Value.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return totalSaved;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
            finally
            {
                AppDbContext.Value.SavedChanges -= OnSavedChanges;
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

        public IAccountRepository AccountRepository => new AccountRepository(AppDbContext);
        public ICurrencyRepository CurrencyRepository => new CurrencyRepository(AppDbContext);
        public IWalletTypeRepository WalletTypeRepository => new WalletTypeRepository(AppDbContext);
        public IWalletRepository WalletRepository => new WalletRepository(AppDbContext);
        public ICategoryRepository CategoryRepository => new CategoryRepository(AppDbContext);
        public ITransactionRepository TransactionRepository => new TransactionRepository(AppDbContext);
        public IAttachmentRepository AttachmentRepository => new AttachmentRepository(AppDbContext);
        public IRateSnapshotRepository RateSnapshotRepository => new RateSnapshotRepository(AppDbContext);
        public IZakatCycleRepository ZakatCycleRepository => new ZakatCycleRepository(AppDbContext);
        #endregion

    }
}
