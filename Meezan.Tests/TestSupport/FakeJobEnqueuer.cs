using Meezan.IServices.IJob;

namespace Meezan.Tests.TestSupport
{
    // Test double for IJobEnqueuer — just records how many times EnqueueRateSync was called, so
    // a test can assert a background sync was (or wasn't) scheduled without any real Hangfire
    // dependency (Meezan.Services never references Hangfire directly; see IJobEnqueuer).
    public class FakeJobEnqueuer : IJobEnqueuer
    {
        public int EnqueueRateSyncCallCount { get; private set; }

        public void EnqueueRateSync() => EnqueueRateSyncCallCount++;
    }
}
