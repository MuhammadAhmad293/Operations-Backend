using Operations.DataModel.Entities;
using Operations.IRepositories.UnitOfWork;
using Operations.IServices.IJob;
using Operations.Services.Setting;

namespace Operations.Services.Job
{
    public class JobService : IJobService
    {
        private IUnitOfWork UnitOfWork { get; }
        private RefreshTokenSettings RefreshTokenSettings { get; }

        public JobService(IUnitOfWork unitOfWork, RefreshTokenSettings refreshTokenSettings)
        {
            UnitOfWork = unitOfWork;
            RefreshTokenSettings = refreshTokenSettings;
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
    }
}
