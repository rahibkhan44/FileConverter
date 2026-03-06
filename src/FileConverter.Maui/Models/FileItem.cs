using CommunityToolkit.Mvvm.ComponentModel;

namespace FileConverter.Maui.Models;

public partial class FileItem : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long Size { get; set; }
    public string FilePath { get; set; } = string.Empty;

    [ObservableProperty]
    private Guid? _jobId;

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private string? _errorMessage;

    public string SizeDisplay
    {
        get
        {
            if (Size < 1024) return $"{Size} B";
            if (Size < 1024 * 1024) return $"{Size / 1024.0:F1} KB";
            return $"{Size / (1024.0 * 1024):F1} MB";
        }
    }

    public string StatusDisplay => Status switch
    {
        "Completed" => "Done",
        "Failed" => ErrorMessage ?? "Failed",
        "Processing" => $"{Progress}%",
        "Pending" => "Queued",
        _ => "Ready"
    };

    public Color StatusColor => Status switch
    {
        "Completed" => Colors.Green,
        "Failed" => Colors.Red,
        "Processing" => Colors.Orange,
        "Pending" => Colors.Gray,
        _ => Colors.DarkGray
    };

    public double ProgressValue => Progress / 100.0;
    public bool IsProcessing => Status == "Processing";
}
