namespace Common.FileStorage
{
    public interface IFileStorageService
    {
        Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken = default);
        Task DeleteAsync(string relativePath);
        Task<Stream> OpenReadAsync(string relativePath);
    }
}
