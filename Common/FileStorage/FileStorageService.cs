namespace Common.FileStorage
{
    // Local-disk storage (Decision D2 from the approved plan — acceptable for the current
    // single-instance local/dev-hosted setup). Files are stored under a year/month subfolder with
    // a GUID-prefixed name to avoid collisions; the returned/stored path is always the relative
    // path (forward-slash separated), never the machine-specific absolute path.
    public class FileStorageService : IFileStorageService
    {
        private readonly FileStorageSettings _settings;

        public FileStorageService(FileStorageSettings settings)
        {
            _settings = settings;
            Directory.CreateDirectory(_settings.UploadRootPath);
        }

        public async Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
        {
            string relativePath = Path.Combine(DateTime.UtcNow.ToString("yyyy/MM"), $"{Guid.NewGuid():N}_{fileName}");
            string fullPath = Path.Combine(_settings.UploadRootPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            await using FileStream fileStream = File.Create(fullPath);
            await content.CopyToAsync(fileStream, cancellationToken);

            return relativePath.Replace('\\', '/');
        }

        public Task DeleteAsync(string relativePath)
        {
            string fullPath = Path.Combine(_settings.UploadRootPath, relativePath);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
            return Task.CompletedTask;
        }

        public Task<Stream> OpenReadAsync(string relativePath)
        {
            string fullPath = Path.Combine(_settings.UploadRootPath, relativePath);
            Stream stream = File.OpenRead(fullPath);
            return Task.FromResult(stream);
        }
    }
}
