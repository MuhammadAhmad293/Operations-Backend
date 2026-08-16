using Hangfire;
using Meezan.IServices.IJob;

namespace Meezan.Jobs
{
    public class HangfireJobEnqueuer : IJobEnqueuer
    {
        private readonly IBackgroundJobClient _backgroundJobClient;

        public HangfireJobEnqueuer(IBackgroundJobClient backgroundJobClient)
        {
            _backgroundJobClient = backgroundJobClient;
        }

        public void EnqueueRateSync()
            => _backgroundJobClient.Enqueue<IJobService>(js => js.SyncRates());
    }
}
