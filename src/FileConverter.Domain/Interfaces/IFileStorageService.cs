namespace FileConverter.Domain.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveUploadedFileAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);
    string GetOutputDirectory(Guid jobId);
    Stream OpenRead(string filePath);
    void DeleteFile(string filePath);
    void CleanupExpiredFiles(TimeSpan maxAge);
    long GetFileSize(string filePath);
    bool FileExists(string filePath);
}
