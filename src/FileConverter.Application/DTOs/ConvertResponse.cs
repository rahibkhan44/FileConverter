namespace FileConverter.Application.DTOs;

public class ConvertResponse
{
    public Guid JobId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string SourceFormat { get; set; } = string.Empty;
    public string TargetFormat { get; set; } = string.Empty;
}

public class BatchConvertResponse
{
    public Guid BatchId { get; set; }
    public int FileCount { get; set; }
    public string TargetFormat { get; set; } = string.Empty;
    public List<ConvertResponse> Jobs { get; set; } = new();
}
