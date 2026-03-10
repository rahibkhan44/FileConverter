using FileConverter.API.Hubs;
using FileConverter.Domain.Enums;
using FileConverter.Domain.Interfaces;
using FileConverter.Infrastructure.Converters;
using FileConverter.Infrastructure.Services;
using Microsoft.AspNetCore.SignalR;

namespace FileConverter.API.BackgroundServices;

public class ConversionWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IJobQueue _jobQueue;
    private readonly IHubContext<ConversionProgressHub> _hubContext;
    private readonly IWebhookService _webhookService;
    private readonly ILogger<ConversionWorker> _logger;
    private readonly SemaphoreSlim _semaphore = new(4);

    public ConversionWorker(
        IServiceProvider serviceProvider,
        IJobQueue jobQueue,
        IHubContext<ConversionProgressHub> hubContext,
        IWebhookService webhookService,
        ILogger<ConversionWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _jobQueue = jobQueue;
        _hubContext = hubContext;
        _webhookService = webhookService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ConversionWorker started — listening for jobs on channel queue");

        await foreach (var jobId in _jobQueue.DequeueAllAsync(stoppingToken))
        {
            await _semaphore.WaitAsync(stoppingToken);

            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessJobAsync(jobId, stoppingToken);
                }
                finally
                {
                    _semaphore.Release();
                }
            }, stoppingToken);
        }
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var jobTracker = scope.ServiceProvider.GetRequiredService<IJobTracker>();
        var factory = scope.ServiceProvider.GetRequiredService<ConversionEngineFactory>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

        var job = jobTracker.GetJob(jobId);
        if (job == null)
        {
            _logger.LogWarning("Job {JobId} not found, skipping", jobId);
            return;
        }

        var jobGroup = _hubContext.Clients.Group($"job-{jobId}");
        var batchGroup = job.BatchJobId.HasValue
            ? _hubContext.Clients.Group($"batch-{job.BatchJobId}")
            : null;

        try
        {
            job.Status = ConversionStatus.Processing;
            job.Progress = 0;
            jobTracker.UpdateJob(job);

            await jobGroup.SendAsync("JobStatusChanged", jobId, "Processing", 0, cancellationToken);

            var converter = factory.GetConverter(job.SourceFormat, job.TargetFormat);
            var outputDir = storage.GetOutputDirectory(job.Id);
            var progress = new Progress<int>(p =>
            {
                job.Progress = p;
                jobTracker.UpdateJob(job);
                _ = jobGroup.SendAsync("ProgressUpdated", jobId, p);
                if (batchGroup != null)
                    _ = batchGroup.SendAsync("BatchJobProgress", job.BatchJobId, jobId, p);
            });

            var outputPath = await converter.ConvertAsync(job.InputFilePath, outputDir, job.TargetFormat,
                job.Options, progress, cancellationToken);

            job.OutputFilePath = outputPath;
            job.Status = ConversionStatus.Completed;
            job.Progress = 100;
            job.CompletedAt = DateTime.UtcNow;
            jobTracker.UpdateJob(job);

            await jobGroup.SendAsync("JobCompleted", jobId, job.OriginalFileName, cancellationToken);
            if (batchGroup != null)
                await batchGroup.SendAsync("BatchJobCompleted", job.BatchJobId, jobId, cancellationToken);

            _logger.LogInformation("Converted {File} from {Source} to {Target}",
                job.OriginalFileName, job.SourceFormat, job.TargetFormat);

            if (!string.IsNullOrEmpty(job.CallbackUrl))
            {
                await _webhookService.SendWebhookAsync(job.CallbackUrl, new
                {
                    jobId = job.Id,
                    status = "Completed",
                    originalFileName = job.OriginalFileName,
                    downloadUrl = $"/api/v1/convert/{job.Id}/download"
                }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            job.Status = ConversionStatus.Failed;
            job.ErrorMessage = ex.Message;
            jobTracker.UpdateJob(job);

            await jobGroup.SendAsync("JobFailed", jobId, ex.Message, cancellationToken);
            if (batchGroup != null)
                await batchGroup.SendAsync("BatchJobFailed", job.BatchJobId, jobId, ex.Message, cancellationToken);

            _logger.LogError(ex, "Failed to convert {File}", job.OriginalFileName);

            if (!string.IsNullOrEmpty(job.CallbackUrl))
            {
                await _webhookService.SendWebhookAsync(job.CallbackUrl, new
                {
                    jobId = job.Id,
                    status = "Failed",
                    originalFileName = job.OriginalFileName,
                    errorMessage = ex.Message
                }, cancellationToken);
            }
        }
    }
}
