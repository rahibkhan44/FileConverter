using FileConverter.Domain.Enums;

namespace FileConverter.Domain.Models;

public class ConversionJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OriginalFileName { get; set; } = string.Empty;
    public string InputFilePath { get; set; } = string.Empty;
    public string? OutputFilePath { get; set; }
    public FileFormat SourceFormat { get; set; }
    public FileFormat TargetFormat { get; set; }
    public ConversionStatus Status { get; set; } = ConversionStatus.Pending;
    public int Progress { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public Dictionary<string, string> Options { get; set; } = new();
    public Guid? BatchJobId { get; set; }
}
