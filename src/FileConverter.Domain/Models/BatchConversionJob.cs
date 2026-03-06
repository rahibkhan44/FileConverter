using FileConverter.Domain.Enums;

namespace FileConverter.Domain.Models;

public class BatchConversionJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public List<ConversionJob> Jobs { get; set; } = new();
    public FileFormat TargetFormat { get; set; }
    public ConversionStatus Status => GetOverallStatus();
    public int OverallProgress => Jobs.Count == 0 ? 0 : (int)Jobs.Average(j => j.Progress);
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    private ConversionStatus GetOverallStatus()
    {
        if (Jobs.Count == 0) return ConversionStatus.Pending;
        if (Jobs.All(j => j.Status == ConversionStatus.Completed)) return ConversionStatus.Completed;
        if (Jobs.Any(j => j.Status == ConversionStatus.Failed) && Jobs.All(j => j.Status is ConversionStatus.Completed or ConversionStatus.Failed))
            return ConversionStatus.Completed;
        if (Jobs.Any(j => j.Status is ConversionStatus.Processing or ConversionStatus.Pending)) return ConversionStatus.Processing;
        return ConversionStatus.Pending;
    }
}
