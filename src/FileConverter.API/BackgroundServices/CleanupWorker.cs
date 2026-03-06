using FileConverter.Domain.Interfaces;

namespace FileConverter.API.BackgroundServices;

public class CleanupWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CleanupWorker> _logger;

    public CleanupWorker(IServiceProvider serviceProvider, ILogger<CleanupWorker> logger)
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
                var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
                var jobTracker = scope.ServiceProvider.GetRequiredService<IJobTracker>();
                var rateLimit = scope.ServiceProvider.GetRequiredService<IRateLimitService>();

                var maxAge = TimeSpan.FromHours(1);
                storage.CleanupExpiredFiles(maxAge);
                jobTracker.CleanupExpired(maxAge);
                rateLimit.ResetExpiredCounters();

                _logger.LogInformation("Cleanup completed at {Time}", DateTime.UtcNow);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
