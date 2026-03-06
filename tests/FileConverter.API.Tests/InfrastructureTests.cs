using FileConverter.Domain.Enums;
using FileConverter.Domain.Models;
using FileConverter.Infrastructure.Services;

namespace FileConverter.API.Tests;

public class InfrastructureTests
{
    [Fact]
    public async Task TempFileStorage_SaveAndRead()
    {
        var storage = new TempFileStorageService();
        var content = "Hello test"u8.ToArray();
        using var stream = new MemoryStream(content);

        var path = await storage.SaveUploadedFileAsync(stream, "test.txt");

        Assert.True(storage.FileExists(path));
        Assert.Equal(content.Length, storage.GetFileSize(path));

        string text;
        using (var readStream = storage.OpenRead(path))
        using (var reader = new StreamReader(readStream))
        {
            text = await reader.ReadToEndAsync();
        }
        Assert.Equal("Hello test", text);

        storage.DeleteFile(path);
        Assert.False(storage.FileExists(path));
    }

    [Fact]
    public void InMemoryJobTracker_AddAndGet()
    {
        var tracker = new InMemoryJobTracker();
        var job = new ConversionJob
        {
            OriginalFileName = "test.txt",
            SourceFormat = FileFormat.Txt,
            TargetFormat = FileFormat.Pdf
        };

        tracker.AddJob(job);
        var retrieved = tracker.GetJob(job.Id);

        Assert.NotNull(retrieved);
        Assert.Equal("test.txt", retrieved.OriginalFileName);
    }

    [Fact]
    public void InMemoryJobTracker_GetPendingJobs()
    {
        var tracker = new InMemoryJobTracker();

        var pending = new ConversionJob { SourceFormat = FileFormat.Txt, TargetFormat = FileFormat.Pdf };
        var processing = new ConversionJob { SourceFormat = FileFormat.Txt, TargetFormat = FileFormat.Pdf, Status = ConversionStatus.Processing };

        tracker.AddJob(pending);
        tracker.AddJob(processing);

        var pendingJobs = tracker.GetPendingJobs().ToList();
        Assert.Single(pendingJobs);
        Assert.Equal(pending.Id, pendingJobs[0].Id);
    }

    [Fact]
    public void RateLimitService_AllowsUpToLimit()
    {
        var service = new RateLimitService();

        for (int i = 0; i < 20; i++)
        {
            Assert.True(service.IsAllowed("test-ip"));
            service.RecordConversion("test-ip");
        }

        Assert.False(service.IsAllowed("test-ip"));
        Assert.True(service.IsAllowed("other-ip")); // Different IP is fine
    }

    [Fact]
    public void BatchConversionJob_StatusCalculation()
    {
        var batch = new BatchConversionJob();
        Assert.Equal(ConversionStatus.Pending, batch.Status);

        batch.Jobs.Add(new ConversionJob { Status = ConversionStatus.Completed, Progress = 100 });
        batch.Jobs.Add(new ConversionJob { Status = ConversionStatus.Processing, Progress = 50 });
        Assert.Equal(ConversionStatus.Processing, batch.Status);
        Assert.Equal(75, batch.OverallProgress);

        batch.Jobs[1].Status = ConversionStatus.Completed;
        batch.Jobs[1].Progress = 100;
        Assert.Equal(ConversionStatus.Completed, batch.Status);
    }
}
