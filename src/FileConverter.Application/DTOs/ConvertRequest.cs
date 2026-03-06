namespace FileConverter.Application.DTOs;

public class ConvertRequest
{
    public string TargetFormat { get; set; } = string.Empty;
    public Dictionary<string, string>? Options { get; set; }
}
