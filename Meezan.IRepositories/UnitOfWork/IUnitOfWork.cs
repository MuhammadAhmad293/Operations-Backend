using Meezan.IRepositories.IRepository;

namespace Meezan.IRepositories.UnitOfWork
{
    public interface IUnitOfWork : IDisposable
    {
        #region Main Methods
        Task<int> CommitAsync(CancellationToken cancellationToken = default);
        Task<int> ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
        #endregion

        #region IRepository
        public IUserRepository UserRepository { get; }
        public IMailRepository MailRepository { get; }
        public IPasswordResetTokenRepository PasswordResetTokenRepository { get; }
        public IOutboxRepository OutboxRepository { get; }
        public IProcessedMessageRepository ProcessedMessageRepository { get; }
        public IRefreshTokenRepository RefreshTokenRepository { get; }

        public IAccountRepository AccountRepository { get; }
        public ICurrencyRepository CurrencyRepository { get; }
        public IWalletTypeRepository WalletTypeRepository { get; }
        public IWalletRepository WalletRepository { get; }
        public ICategoryRepository CategoryRepository { get; }
        public ITransactionRepository TransactionRepository { get; }
        public IAttachmentRepository AttachmentRepository { get; }
        public IRateSnapshotRepository RateSnapshotRepository { get; }
        public IZakatCycleRepository ZakatCycleRepository { get; }
        #endregion
    }
}
