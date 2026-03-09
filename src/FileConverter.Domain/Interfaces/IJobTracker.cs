using FileConverter.Domain.Models;

namespace FileConverter.Domain.Interfaces;

public interface IJobTracker
{
    void AddJob(ConversionJob job);
    ConversionJob? GetJob(Guid id);
    void AddBatchJob(BatchConversionJob batch);
    BatchConversionJob? GetBatchJob(Guid id);
    void CleanupExpired(TimeSpan maxAge);
    IEnumerable<ConversionJob> GetPendingJobs();
    void UpdateJob(ConversionJob job);
}
