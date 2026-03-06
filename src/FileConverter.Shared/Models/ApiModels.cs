namespace FileConverter.Shared.Models;

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

public class FormatInfoResponse
{
    public string Format { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> TargetFormats { get; set; } = new();
}

public class FormatOptionInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string DefaultValue { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string>? AllowedValues { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
}

public class ApiError
{
    public string Error { get; set; } = string.Empty;
}
