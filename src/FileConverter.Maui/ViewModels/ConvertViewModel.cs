using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileConverter.Maui.Models;
using FileConverter.Shared;
using System.Collections.ObjectModel;

namespace FileConverter.Maui.ViewModels;

public partial class ConvertViewModel : ObservableObject
{
    private readonly FileConverterApiClient _apiClient;

    public ConvertViewModel(FileConverterApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public ObservableCollection<FileItem> Files { get; } = new();
    public ObservableCollection<string> AvailableTargets { get; } = new();

    [ObservableProperty]
    private string? _selectedTarget;

    [ObservableProperty]
    private bool _isConverting;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private Guid? _batchId;

    [ObservableProperty]
    private bool _hasCompletedFiles;

    [RelayCommand]
    private async Task PickFiles()
    {
        try
        {
            var results = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "Select files to convert"
            });

            if (results == null) return;

            foreach (var result in results)
            {
                var ext = Path.GetExtension(result.FileName);
                if (!ClientSupportedFormats.IsSupported(ext)) continue;

                var fileInfo = new System.IO.FileInfo(result.FullPath);
                Files.Add(new FileItem
                {
                    Name = result.FileName,
                    Extension = ext.TrimStart('.'),
                    Size = fileInfo.Length,
                    FilePath = result.FullPath
                });
            }

            UpdateAvailableTargets();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void UpdateAvailableTargets()
    {
        AvailableTargets.Clear();
        if (Files.Count == 0) return;

        HashSet<string>? common = null;
        foreach (var file in Files)
        {
            var targets = ClientSupportedFormats.GetTargetFormats(file.Extension).ToHashSet();
            common = common == null ? targets : new HashSet<string>(common.Intersect(targets));
        }

        if (common != null)
        {
            foreach (var t in common.OrderBy(f => f))
                AvailableTargets.Add(t);
        }

        if (SelectedTarget == null || !AvailableTargets.Contains(SelectedTarget))
            SelectedTarget = AvailableTargets.FirstOrDefault();
    }

    [RelayCommand]
    private async Task ConvertAll()
    {
        if (string.IsNullOrEmpty(SelectedTarget) || Files.Count == 0) return;

        IsConverting = true;
        ErrorMessage = null;

        try
        {
            foreach (var file in Files)
                file.Status = "Pending";

            if (Files.Count == 1)
            {
                var file = Files[0];
                using var stream = File.OpenRead(file.FilePath);
                var response = await _apiClient.ConvertFileAsync(stream, file.Name, SelectedTarget);
                file.JobId = response.JobId;
                await PollSingleJob(file);
            }
            else
            {
                var fileStreams = Files.Select(f => ((Stream)File.OpenRead(f.FilePath), f.Name)).ToList();
                var batchResponse = await _apiClient.ConvertBatchAsync(fileStreams, SelectedTarget);
                BatchId = batchResponse.BatchId;

                for (int i = 0; i < batchResponse.Jobs.Count && i < Files.Count; i++)
                    Files[i].JobId = batchResponse.Jobs[i].JobId;

                foreach (var (stream, _) in fileStreams)
                    stream.Dispose();

                await PollBatchStatus();
            }
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Conversion failed: {ex.Message}";
        }
        finally
        {
            IsConverting = false;
            HasCompletedFiles = Files.Any(f => f.Status == "Completed");
        }
    }

    private async Task PollSingleJob(FileItem file)
    {
        while (file.JobId.HasValue)
        {
            try
            {
                var status = await _apiClient.GetJobStatusAsync(file.JobId.Value);
                file.Status = status.Status;
                file.Progress = status.Progress;
                file.ErrorMessage = status.ErrorMessage;

                if (status.Status is "Completed" or "Failed") break;
                await Task.Delay(1000);
            }
            catch { break; }
        }
    }

    private async Task PollBatchStatus()
    {
        if (!BatchId.HasValue) return;

        while (true)
        {
            try
            {
                var status = await _apiClient.GetBatchStatusAsync(BatchId.Value);

                foreach (var fileStatus in status.Files)
                {
                    var file = Files.FirstOrDefault(f => f.JobId == fileStatus.JobId);
                    if (file != null)
                    {
                        file.Status = fileStatus.Status;
                        file.Progress = fileStatus.Progress;
                        file.ErrorMessage = fileStatus.ErrorMessage;
                    }
                }

                if (status.Status is "Completed" or "Failed") break;
                await Task.Delay(1000);
            }
            catch { break; }
        }
    }

    [RelayCommand]
    private async Task DownloadFile(FileItem file)
    {
        if (!file.JobId.HasValue) return;

        try
        {
            var (stream, fileName) = await _apiClient.DownloadFileAsync(file.JobId.Value);

#if ANDROID || IOS
            var targetPath = Path.Combine(FileSystem.CacheDirectory, fileName);
#else
            var targetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);
#endif

            using var fileStream = File.Create(targetPath);
            await stream.CopyToAsync(fileStream);

            await Application.Current!.Windows[0].Page!.DisplayAlert("Downloaded", $"Saved to: {targetPath}", "OK");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Download failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DownloadAllZip()
    {
        if (!BatchId.HasValue) return;

        try
        {
            var stream = await _apiClient.DownloadBatchZipAsync(BatchId.Value);

#if ANDROID || IOS
            var targetPath = Path.Combine(FileSystem.CacheDirectory, "converted_files.zip");
#else
            var targetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "converted_files.zip");
#endif

            using var fileStream = File.Create(targetPath);
            await stream.CopyToAsync(fileStream);

            await Application.Current!.Windows[0].Page!.DisplayAlert("Downloaded", $"Saved to: {targetPath}", "OK");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Download failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void RemoveFile(FileItem file)
    {
        Files.Remove(file);
        UpdateAvailableTargets();
    }

    [RelayCommand]
    private void ClearFiles()
    {
        Files.Clear();
        AvailableTargets.Clear();
        SelectedTarget = null;
        BatchId = null;
        HasCompletedFiles = false;
    }
}
