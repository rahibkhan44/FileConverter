using FileConverter.Application;
using FileConverter.Domain.Enums;
using FileConverter.Domain.Interfaces;
using System.Diagnostics;

namespace FileConverter.Infrastructure.Converters;

public class PdfToImageConverter : IFileConverter
{
    private static readonly HashSet<FileFormat> SupportedTargets = new()
    {
        FileFormat.Png, FileFormat.Jpg
    };

    public bool CanConvert(FileFormat source, FileFormat target)
        => source == FileFormat.Pdf && SupportedTargets.Contains(target);

    public async Task<string> ConvertAsync(string inputPath, string outputDirectory, FileFormat targetFormat,
        Dictionary<string, string> options, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(10);

        // Use LibreOffice to convert PDF to image
        var sofficePath = FindSofficePath();
        if (sofficePath == null)
            throw new InvalidOperationException("LibreOffice is required for PDF to image conversion. Install from https://www.libreoffice.org/download/");

        Directory.CreateDirectory(outputDirectory);

        var loFormat = targetFormat == FileFormat.Jpg ? "jpg" : "png";
        var args = $"--headless --norestore --convert-to {loFormat} --outdir \"{outputDirectory}\" \"{inputPath}\"";

        progress?.Report(20);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = sofficePath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = outputDirectory
            }
        };

        process.Start();
        progress?.Report(50);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(5));

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { }
            throw new TimeoutException("PDF to image conversion timed out after 5 minutes.");
        }

        progress?.Report(80);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"PDF to image conversion failed (exit code {process.ExitCode}): {error}");
        }

        var expectedName = Path.GetFileNameWithoutExtension(inputPath) + "." + loFormat;
        var outputPath = Path.Combine(outputDirectory, expectedName);

        if (!File.Exists(outputPath))
        {
            var candidates = Directory.GetFiles(outputDirectory, $"*.{loFormat}");
            outputPath = candidates.FirstOrDefault()
                ?? throw new FileNotFoundException($"PDF to image conversion did not produce output. Expected: {expectedName}");
        }

        progress?.Report(100);
        return outputPath;
    }

    private static string? FindSofficePath()
    {
        string[] windowsPaths =
        [
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            @"C:\Program Files (x86)\LibreOffice\program\soffice.exe",
        ];

        foreach (var path in windowsPaths)
        {
            if (File.Exists(path)) return path;
        }

        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "which",
                Arguments = "soffice",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process != null)
            {
                var result = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                if (!string.IsNullOrEmpty(result) && File.Exists(result))
                    return result;
            }
        }
        catch { }

        if (File.Exists("/Applications/LibreOffice.app/Contents/MacOS/soffice"))
            return "/Applications/LibreOffice.app/Contents/MacOS/soffice";

        return null;
    }
}
