namespace FileConverter.Application.DTOs;

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
