namespace FileConverter.Application.DTOs;

public class JobStatusResponse
{
    public Guid JobId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string? ErrorMessage { get; set; }
    public string SourceFormat { get; set; } = string.Empty;
    public string TargetFormat { get; set; } = string.Empty;
}

public class BatchStatusResponse
{
    public Guid BatchId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int OverallProgress { get; set; }
    public List<JobStatusResponse> Files { get; set; } = new();
}
