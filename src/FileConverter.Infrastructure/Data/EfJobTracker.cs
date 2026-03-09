using FileConverter.Domain.Enums;
using FileConverter.Domain.Interfaces;
using FileConverter.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace FileConverter.Infrastructure.Data;

public class EfJobTracker : IJobTracker
{
    private readonly AppDbContext _db;

    public EfJobTracker(AppDbContext db)
    {
        _db = db;
    }

    public void AddJob(ConversionJob job)
    {
        _db.Jobs.Add(job);
        _db.SaveChanges();
    }

    public ConversionJob? GetJob(Guid id)
    {
        return _db.Jobs.FirstOrDefault(j => j.Id == id);
    }

    public void AddBatchJob(BatchConversionJob batch)
    {
        _db.BatchJobs.Add(batch);
        _db.SaveChanges();
    }

    public BatchConversionJob? GetBatchJob(Guid id)
    {
        return _db.BatchJobs
            .Include(b => b.Jobs)
            .FirstOrDefault(b => b.Id == id);
    }

    public void CleanupExpired(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;

        var expiredJobs = _db.Jobs.Where(j => j.CreatedAt < cutoff);
        _db.Jobs.RemoveRange(expiredJobs);

        var expiredBatches = _db.BatchJobs.Where(b => b.CreatedAt < cutoff);
        _db.BatchJobs.RemoveRange(expiredBatches);

        _db.SaveChanges();
    }

    public IEnumerable<ConversionJob> GetPendingJobs()
    {
        return _db.Jobs
            .Where(j => j.Status == ConversionStatus.Pending)
            .OrderBy(j => j.CreatedAt)
            .ToList();
    }

    public void UpdateJob(ConversionJob job)
    {
        _db.SaveChanges();
    }
}
