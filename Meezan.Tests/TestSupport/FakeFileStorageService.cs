using Common.FileStorage;

namespace Meezan.Tests.TestSupport
{
    // No test in this suite exercises attachments; this only exists because
    // TransactionService's constructor requires an IFileStorageService.
    public class FakeFileStorageService : IFileStorageService
    {
        public Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not used by these tests.");

        public Task DeleteAsync(string relativePath) => Task.CompletedTask;

        public Task<Stream> OpenReadAsync(string relativePath)
            => throw new NotSupportedException("Not used by these tests.");
    }
}
