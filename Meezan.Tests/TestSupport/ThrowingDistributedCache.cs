using Microsoft.Extensions.Caching.Distributed;

namespace Meezan.Tests.TestSupport
{
    // Simulates a genuinely-down Redis: every operation throws, the way
    // StackExchange.Redis's IDistributedCache implementation throws while disconnected. Used to
    // prove RateService's cache-aside reads/writes actually degrade to the DB rather than
    // propagating, instead of just asserting against an empty (but reachable) FakeDistributedCache.
    public class ThrowingDistributedCache : IDistributedCache
    {
        private static InvalidOperationException Down() => new("Simulated Redis outage");

        public byte[]? Get(string key) => throw Down();

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw Down();

        public void Refresh(string key) => throw Down();

        public Task RefreshAsync(string key, CancellationToken token = default) => throw Down();

        public void Remove(string key) => throw Down();

        public Task RemoveAsync(string key, CancellationToken token = default) => throw Down();

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Down();

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw Down();
    }
}
