using Meezan.DataModel.Entities;
using Meezan.IRepositories.UnitOfWork;
using Meezan.IServices.IJob;
using Meezan.IServices.IService;
using Meezan.Services.Setting;
using Microsoft.Extensions.Logging;

namespace Meezan.Services.Job
{
    public class JobService : IJobService
    {
        private IUnitOfWork UnitOfWork { get; }
        private RefreshTokenSettings RefreshTokenSettings { get; }
        private IRateService RateService { get; }
        private IZakatEngine ZakatEngine { get; }
        private ILogger<JobService> Logger { get; }

        public JobService(IUnitOfWork unitOfWork, RefreshTokenSettings refreshTokenSettings, IRateService rateService,
            IZakatEngine zakatEngine, ILogger<JobService> logger)
        {
            UnitOfWork = unitOfWork;
            RefreshTokenSettings = refreshTokenSettings;
            RateService = rateService;
            ZakatEngine = zakatEngine;
            Logger = logger;
        }

        public void FireAndForgetJob()
        {
            Console.WriteLine("Hello from a Fire and Forget job!");
        }
        public void ReccuringJob()
        {
            Console.WriteLine("Hello from a Scheduled job!");
        }
        public void DelayedJob()
        {
            Console.WriteLine("Hello from a Delayed job!");
        }
        public void ContinuationJob()
        {
            Console.WriteLine("Hello from a Continuation job!");
        }

        public void CleanupExpiredRefreshTokens()
            => CleanupExpiredRefreshTokensAsync().GetAwaiter().GetResult();

        private async Task CleanupExpiredRefreshTokensAsync()
        {
            DateTime cutoff = DateTime.UtcNow.AddDays(-RefreshTokenSettings.RetentionDays);
            List<RefreshToken> stale = await UnitOfWork.RefreshTokenRepository.GetOlderThanForCleanupAsync(cutoff);
            if (stale.Count == 0)
                return;

            stale.ForEach(t => UnitOfWork.RefreshTokenRepository.Delete(t));
            await UnitOfWork.CommitAsync();
        }

        public void SyncRates()
            => SyncRatesAsync().GetAwaiter().GetResult();

        private async Task SyncRatesAsync()
        {
            await RateService.SyncAsync();

            // BR-13/14/15: a fresh rate can move a pot across the nisab threshold or a hawl
            // across its due boundary purely from price movement, with no transaction of its
            // own to trigger ReevaluateAsync — so every account needs a daily re-check here,
            // not just the transaction-triggered path already wired into TransactionService.
            List<Account> accounts = await UnitOfWork.AccountRepository.GetAllAsync();
            foreach (Account account in accounts)
            {
                try
                {
                    // ReevaluateAsync only stages changes (Create/Update) — every other call
                    // site persists them by running inside ExecuteInTransactionAsync, which
                    // SaveChanges-then-commits after the delegate returns. Same requirement
                    // here, per-account, so one account's writes can never ride along on
                    // another's flush (or worse, never get flushed at all if it's the last one).
                    await UnitOfWork.ExecuteInTransactionAsync(async ct => await ZakatEngine.ReevaluateAsync(account.Id, ct));
                }
                catch (Exception ex)
                {
                    // One account's bad state must not stop the hawl re-check for everyone
                    // else — same per-item isolation OutboxPublisherService uses for its batch.
                    Logger.LogError(ex, "ZakatEngine.ReevaluateAsync failed for AccountId={AccountId} during daily rate sync", account.Id);
                }
            }
        }
    }
}
