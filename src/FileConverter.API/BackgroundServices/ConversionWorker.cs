using FileConverter.Domain.Enums;
using FileConverter.Domain.Interfaces;
using FileConverter.Infrastructure.Converters;

namespace FileConverter.API.BackgroundServices;

public class ConversionWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ConversionWorker> _logger;
    private readonly SemaphoreSlim _semaphore = new(4); // Max 4 concurrent conversions

    public ConversionWorker(IServiceProvider serviceProvider, ILogger<ConversionWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var jobTracker = scope.ServiceProvider.GetRequiredService<IJobTracker>();
                var factory = scope.ServiceProvider.GetRequiredService<ConversionEngineFactory>();
                var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

                var pendingJobs = jobTracker.GetPendingJobs().ToList();

                if (pendingJobs.Count == 0)
                {
                    await Task.Delay(500, stoppingToken);
                    continue;
                }

                var tasks = pendingJobs.Select(async job =>
                {
                    await _semaphore.WaitAsync(stoppingToken);
                    try
                    {
                        await ProcessJobAsync(job, factory, storage, stoppingToken);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in conversion worker");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private async Task ProcessJobAsync(Domain.Models.ConversionJob job, ConversionEngineFactory factory,
        IFileStorageService storage, CancellationToken cancellationToken)
    {
        try
        {
            job.Status = ConversionStatus.Processing;
            job.Progress = 0;

            var converter = factory.GetConverter(job.SourceFormat, job.TargetFormat);
            var outputDir = storage.GetOutputDirectory(job.Id);
            var progress = new Progress<int>(p => job.Progress = p);

            var outputPath = await converter.ConvertAsync(job.InputFilePath, outputDir, job.TargetFormat,
                job.Options, progress, cancellationToken);

            job.OutputFilePath = outputPath;
            job.Status = ConversionStatus.Completed;
            job.Progress = 100;
            job.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation("Converted {File} from {Source} to {Target}",
                job.OriginalFileName, job.SourceFormat, job.TargetFormat);
        }
        catch (Exception ex)
        {
            job.Status = ConversionStatus.Failed;
            job.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Failed to convert {File}", job.OriginalFileName);
        }
    }
}
