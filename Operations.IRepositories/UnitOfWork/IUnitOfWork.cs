using Operations.IRepositories.IRepository;

namespace Operations.IRepositories.UnitOfWork
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
        #endregion
    }
}
