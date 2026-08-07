namespace Meezan.IServices.IJob
{
    public interface IJobService
    {
        void FireAndForgetJob();
        void ReccuringJob();
        void DelayedJob();
        void ContinuationJob();
        void CleanupExpiredRefreshTokens();
    }
}
