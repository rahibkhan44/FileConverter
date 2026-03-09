using FileConverter.Domain.Enums;
using FileConverter.Domain.Interfaces;
using FileConverter.Domain.Models;
using System.Collections.Concurrent;

namespace FileConverter.Infrastructure.Services;

public class InMemoryJobTracker : IJobTracker
{
    private readonly ConcurrentDictionary<Guid, ConversionJob> _jobs = new();
    private readonly ConcurrentDictionary<Guid, BatchConversionJob> _batchJobs = new();

    public void AddJob(ConversionJob job) => _jobs[job.Id] = job;

    public ConversionJob? GetJob(Guid id) => _jobs.TryGetValue(id, out var job) ? job : null;

    public void AddBatchJob(BatchConversionJob batch) => _batchJobs[batch.Id] = batch;

    public BatchConversionJob? GetBatchJob(Guid id) => _batchJobs.TryGetValue(id, out var batch) ? batch : null;

    public void CleanupExpired(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;

        var expiredJobs = _jobs.Where(kv => kv.Value.CreatedAt < cutoff).Select(kv => kv.Key).ToList();
        foreach (var id in expiredJobs)
            _jobs.TryRemove(id, out _);

        var expiredBatches = _batchJobs.Where(kv => kv.Value.CreatedAt < cutoff).Select(kv => kv.Key).ToList();
        foreach (var id in expiredBatches)
            _batchJobs.TryRemove(id, out _);
    }

    public IEnumerable<ConversionJob> GetPendingJobs()
    {
        return _jobs.Values.Where(j => j.Status == ConversionStatus.Pending).OrderBy(j => j.CreatedAt);
    }

    public void UpdateJob(ConversionJob job)
    {
        // In-memory: object is already mutated in place, nothing to persist
        _jobs[job.Id] = job;
    }
}
