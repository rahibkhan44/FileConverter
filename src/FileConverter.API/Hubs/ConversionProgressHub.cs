using Microsoft.AspNetCore.SignalR;

namespace FileConverter.API.Hubs;

public class ConversionProgressHub : Hub
{
    private readonly ILogger<ConversionProgressHub> _logger;

    public ConversionProgressHub(ILogger<ConversionProgressHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Client subscribes to progress updates for a specific job.
    /// </summary>
    public async Task SubscribeToJob(Guid jobId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"job-{jobId}");
        _logger.LogDebug("Client {ConnectionId} subscribed to job {JobId}", Context.ConnectionId, jobId);
    }

    /// <summary>
    /// Client subscribes to all jobs in a batch.
    /// </summary>
    public async Task SubscribeToBatch(Guid batchId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"batch-{batchId}");
        _logger.LogDebug("Client {ConnectionId} subscribed to batch {BatchId}", Context.ConnectionId, batchId);
    }

    public override Task OnConnectedAsync()
    {
        _logger.LogDebug("SignalR client connected: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("SignalR client disconnected: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
