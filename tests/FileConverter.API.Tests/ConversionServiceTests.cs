using FileConverter.Application;
using FileConverter.Application.Services;
using FileConverter.Domain.Enums;
using FileConverter.Domain.Interfaces;
using FileConverter.Infrastructure.Services;

namespace FileConverter.API.Tests;

public class ConversionServiceTests
{
    private ConversionService CreateService()
    {
        var storage = new TempFileStorageService();
        var jobTracker = new InMemoryJobTracker();
        var rateLimit = new RateLimitService();
        return new ConversionService(storage, jobTracker, rateLimit);
    }

    [Fact]
    public async Task CreateJobAsync_ValidFile_ReturnsJobId()
    {
        var service = CreateService();
        var content = "Hello, world!"u8.ToArray();
        using var stream = new MemoryStream(content);

        var result = await service.CreateJobAsync(stream, "test.txt", "pdf", null, "127.0.0.1", CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.JobId);
        Assert.Equal("test.txt", result.FileName);
        Assert.Equal("txt", result.SourceFormat);
        Assert.Equal("pdf", result.TargetFormat);
    }

    [Fact]
    public async Task CreateJobAsync_UnsupportedFormat_Throws()
    {
        var service = CreateService();
        using var stream = new MemoryStream("test"u8.ToArray());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateJobAsync(stream, "test.xyz", "pdf", null, "127.0.0.1", CancellationToken.None));
    }

    [Fact]
    public async Task CreateJobAsync_InvalidConversion_Throws()
    {
        var service = CreateService();
        using var stream = new MemoryStream("test"u8.ToArray());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateJobAsync(stream, "test.png", "docx", null, "127.0.0.1", CancellationToken.None));
    }

    [Fact]
    public async Task CreateJobAsync_RateLimitExceeded_Throws()
    {
        var storage = new TempFileStorageService();
        var jobTracker = new InMemoryJobTracker();
        var rateLimit = new RateLimitService();
        var service = new ConversionService(storage, jobTracker, rateLimit);

        for (int i = 0; i < 20; i++)
        {
            using var s = new MemoryStream("test"u8.ToArray());
            await service.CreateJobAsync(s, "test.txt", "pdf", null, "10.0.0.1", CancellationToken.None);
        }

        using var stream = new MemoryStream("test"u8.ToArray());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateJobAsync(stream, "test.txt", "pdf", null, "10.0.0.1", CancellationToken.None));
    }

    [Fact]
    public async Task CreateBatchJobAsync_MultiplFiles_ReturnsBatchId()
    {
        var service = CreateService();
        var files = new List<(Stream, string)>
        {
            (new MemoryStream("test1"u8.ToArray()), "file1.txt"),
            (new MemoryStream("test2"u8.ToArray()), "file2.txt"),
        };

        var result = await service.CreateBatchJobAsync(files, "pdf", null, "127.0.0.1", CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.BatchId);
        Assert.Equal(2, result.FileCount);
        Assert.Equal(2, result.Jobs.Count);

        foreach (var (s, _) in files) s.Dispose();
    }

    [Fact]
    public async Task CreateBatchJobAsync_TooManyFiles_Throws()
    {
        var service = CreateService();
        var files = Enumerable.Range(0, 101)
            .Select(i => ((Stream)new MemoryStream("test"u8.ToArray()), $"file{i}.txt"))
            .ToList();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateBatchJobAsync(files, "pdf", null, "127.0.0.1", CancellationToken.None));

        foreach (var (s, _) in files) s.Dispose();
    }
}
