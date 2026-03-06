using FileConverter.Domain.Interfaces;

namespace FileConverter.Infrastructure.Services;

public class TempFileStorageService : IFileStorageService
{
    private readonly string _basePath;

    public TempFileStorageService(string? basePath = null)
    {
        _basePath = basePath ?? Path.Combine(Path.GetTempPath(), "FileConverter");
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> SaveUploadedFileAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        var jobDir = Path.Combine(_basePath, "uploads", Guid.NewGuid().ToString());
        Directory.CreateDirectory(jobDir);

        var sanitizedName = Path.GetFileName(fileName);
        var filePath = Path.Combine(jobDir, sanitizedName);

        using var fs = new FileStream(filePath, FileMode.Create);
        await fileStream.CopyToAsync(fs, cancellationToken);

        return filePath;
    }

    public string GetOutputDirectory(Guid jobId)
    {
        var dir = Path.Combine(_basePath, "output", jobId.ToString());
        Directory.CreateDirectory(dir);
        return dir;
    }

    public Stream OpenRead(string filePath) => new FileStream(filePath, FileMode.Open, FileAccess.Read);

    public void DeleteFile(string filePath)
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
    }

    public void CleanupExpiredFiles(TimeSpan maxAge)
    {
        CleanupDirectory(Path.Combine(_basePath, "uploads"), maxAge);
        CleanupDirectory(Path.Combine(_basePath, "output"), maxAge);
    }

    public long GetFileSize(string filePath) => new FileInfo(filePath).Length;

    public bool FileExists(string filePath) => File.Exists(filePath);

    private static void CleanupDirectory(string path, TimeSpan maxAge)
    {
        if (!Directory.Exists(path)) return;

        foreach (var dir in Directory.GetDirectories(path))
        {
            var info = new DirectoryInfo(dir);
            if (DateTime.UtcNow - info.CreationTimeUtc > maxAge)
            {
                try { Directory.Delete(dir, true); } catch { /* best effort */ }
            }
        }
    }
}
