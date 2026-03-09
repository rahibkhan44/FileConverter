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

public class PageCountResponse
{
    public int PageCount { get; set; }
    public string FileName { get; set; } = string.Empty;
}

public class PdfMetadataResponse
{
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? Subject { get; set; }
    public string? Keywords { get; set; }
    public string? Creator { get; set; }
    public string? Producer { get; set; }
    public int PageCount { get; set; }
}

public class ApiError
{
    public string Error { get; set; } = string.Empty;
}

// Auth models
public class AuthRegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AuthLoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class UserProfileResponse
{
    public string Email { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public int DailyConversionLimit { get; set; }
    public long MaxFileSizeMB { get; set; }
    public int TotalConversions { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ConversionHistoryItem
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string SourceFormat { get; set; } = string.Empty;
    public string TargetFormat { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class ApiKeyInfo
{
    public Guid Id { get; set; }
    public string KeyPrefix { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class ApiKeyCreateResponse
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
