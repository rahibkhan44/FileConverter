using FileConverter.Application;
using FileConverter.Domain.Enums;
using FileConverter.Domain.Interfaces;
using System.Diagnostics;

namespace FileConverter.Infrastructure.Converters;

public class LibreOfficeConverter : IFileConverter
{
    private readonly string? _sofficePath;

    // All conversions LibreOffice handles — comprehensive any-to-any within categories
    private static readonly Dictionary<(FileFormat Source, FileFormat Target), string> ConversionMap = new()
    {
        // ═══════════════════════════════════════════
        // DOCUMENT conversions — any doc to any doc
        // ═══════════════════════════════════════════

        // DOCX →
        [(FileFormat.Docx, FileFormat.Pdf)]  = "pdf",
        [(FileFormat.Docx, FileFormat.Doc)]  = "doc",
        [(FileFormat.Docx, FileFormat.Odt)]  = "odt",
        [(FileFormat.Docx, FileFormat.Rtf)]  = "rtf",
        [(FileFormat.Docx, FileFormat.Txt)]  = "txt",
        [(FileFormat.Docx, FileFormat.Html)] = "html",

        // DOC →
        [(FileFormat.Doc, FileFormat.Pdf)]   = "pdf",
        [(FileFormat.Doc, FileFormat.Docx)]  = "docx",
        [(FileFormat.Doc, FileFormat.Odt)]   = "odt",
        [(FileFormat.Doc, FileFormat.Rtf)]   = "rtf",
        [(FileFormat.Doc, FileFormat.Txt)]   = "txt",
        [(FileFormat.Doc, FileFormat.Html)]  = "html",

        // ODT →
        [(FileFormat.Odt, FileFormat.Pdf)]   = "pdf",
        [(FileFormat.Odt, FileFormat.Docx)]  = "docx",
        [(FileFormat.Odt, FileFormat.Doc)]   = "doc",
        [(FileFormat.Odt, FileFormat.Rtf)]   = "rtf",
        [(FileFormat.Odt, FileFormat.Txt)]   = "txt",
        [(FileFormat.Odt, FileFormat.Html)]  = "html",

        // RTF →
        [(FileFormat.Rtf, FileFormat.Pdf)]   = "pdf",
        [(FileFormat.Rtf, FileFormat.Docx)]  = "docx",
        [(FileFormat.Rtf, FileFormat.Doc)]   = "doc",
        [(FileFormat.Rtf, FileFormat.Odt)]   = "odt",
        [(FileFormat.Rtf, FileFormat.Txt)]   = "txt",
        [(FileFormat.Rtf, FileFormat.Html)]  = "html",

        // TXT →
        [(FileFormat.Txt, FileFormat.Pdf)]   = "pdf",
        [(FileFormat.Txt, FileFormat.Docx)]  = "docx",
        [(FileFormat.Txt, FileFormat.Doc)]   = "doc",
        [(FileFormat.Txt, FileFormat.Odt)]   = "odt",
        [(FileFormat.Txt, FileFormat.Rtf)]   = "rtf",
        [(FileFormat.Txt, FileFormat.Html)]  = "html",

        // HTML →
        [(FileFormat.Html, FileFormat.Pdf)]  = "pdf",
        [(FileFormat.Html, FileFormat.Docx)] = "docx",
        [(FileFormat.Html, FileFormat.Doc)]  = "doc",
        [(FileFormat.Html, FileFormat.Odt)]  = "odt",
        [(FileFormat.Html, FileFormat.Rtf)]  = "rtf",
        [(FileFormat.Html, FileFormat.Txt)]  = "txt",

        // ═══════════════════════════════════════════
        // PDF → documents (LibreOffice can export PDF to doc formats)
        // ═══════════════════════════════════════════
        [(FileFormat.Pdf, FileFormat.Docx)]  = "docx",
        [(FileFormat.Pdf, FileFormat.Doc)]   = "doc",
        [(FileFormat.Pdf, FileFormat.Odt)]   = "odt",
        [(FileFormat.Pdf, FileFormat.Rtf)]   = "rtf",
        [(FileFormat.Pdf, FileFormat.Txt)]   = "txt",
        [(FileFormat.Pdf, FileFormat.Html)]  = "html",
        // PDF → images handled by PdfToImageConverter

        // ═══════════════════════════════════════════
        // SPREADSHEET conversions — any spreadsheet to any spreadsheet
        // ═══════════════════════════════════════════

        // XLSX →
        [(FileFormat.Xlsx, FileFormat.Pdf)]  = "pdf",
        [(FileFormat.Xlsx, FileFormat.Xls)]  = "xls",
        [(FileFormat.Xlsx, FileFormat.Ods)]  = "ods",
        [(FileFormat.Xlsx, FileFormat.Csv)]  = "csv",
        [(FileFormat.Xlsx, FileFormat.Html)] = "html",
        [(FileFormat.Xlsx, FileFormat.Txt)]  = "txt",

        // XLS →
        [(FileFormat.Xls, FileFormat.Pdf)]   = "pdf",
        [(FileFormat.Xls, FileFormat.Xlsx)]  = "xlsx",
        [(FileFormat.Xls, FileFormat.Ods)]   = "ods",
        [(FileFormat.Xls, FileFormat.Csv)]   = "csv",
        [(FileFormat.Xls, FileFormat.Html)]  = "html",
        [(FileFormat.Xls, FileFormat.Txt)]   = "txt",

        // ODS →
        [(FileFormat.Ods, FileFormat.Pdf)]   = "pdf",
        [(FileFormat.Ods, FileFormat.Xlsx)]  = "xlsx",
        [(FileFormat.Ods, FileFormat.Xls)]   = "xls",
        [(FileFormat.Ods, FileFormat.Csv)]   = "csv",
        [(FileFormat.Ods, FileFormat.Html)]  = "html",
        [(FileFormat.Ods, FileFormat.Txt)]   = "txt",

        // CSV →
        [(FileFormat.Csv, FileFormat.Pdf)]   = "pdf",
        [(FileFormat.Csv, FileFormat.Xlsx)]  = "xlsx",
        [(FileFormat.Csv, FileFormat.Xls)]   = "xls",
        [(FileFormat.Csv, FileFormat.Ods)]   = "ods",
        [(FileFormat.Csv, FileFormat.Html)]  = "html",

        // ═══════════════════════════════════════════
        // PRESENTATION conversions — any to any
        // ═══════════════════════════════════════════

        // PPTX →
        [(FileFormat.Pptx, FileFormat.Pdf)]  = "pdf",
        [(FileFormat.Pptx, FileFormat.Ppt)]  = "ppt",
        [(FileFormat.Pptx, FileFormat.Odp)]  = "odp",
        [(FileFormat.Pptx, FileFormat.Txt)]  = "txt",
        [(FileFormat.Pptx, FileFormat.Html)] = "html",

        // PPT →
        [(FileFormat.Ppt, FileFormat.Pdf)]   = "pdf",
        [(FileFormat.Ppt, FileFormat.Pptx)]  = "pptx",
        [(FileFormat.Ppt, FileFormat.Odp)]   = "odp",
        [(FileFormat.Ppt, FileFormat.Txt)]   = "txt",
        [(FileFormat.Ppt, FileFormat.Html)]  = "html",

        // ODP →
        [(FileFormat.Odp, FileFormat.Pdf)]   = "pdf",
        [(FileFormat.Odp, FileFormat.Pptx)]  = "pptx",
        [(FileFormat.Odp, FileFormat.Ppt)]   = "ppt",
        [(FileFormat.Odp, FileFormat.Txt)]   = "txt",
        [(FileFormat.Odp, FileFormat.Html)]  = "html",
    };

    public LibreOfficeConverter()
    {
        _sofficePath = FindSofficePath();
    }

    public bool CanConvert(FileFormat source, FileFormat target)
    {
        return _sofficePath != null && ConversionMap.ContainsKey((source, target));
    }

    public async Task<string> ConvertAsync(string inputPath, string outputDirectory, FileFormat targetFormat,
        Dictionary<string, string> options, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_sofficePath == null)
            throw new InvalidOperationException("LibreOffice is not installed. Install it from https://www.libreoffice.org/download/");

        if (!ConversionMap.TryGetValue((SupportedConversions.ParseFormat(Path.GetExtension(inputPath))!.Value, targetFormat), out var loFormat))
            throw new NotSupportedException($"LibreOffice cannot convert to {targetFormat}");

        progress?.Report(10);

        // LibreOffice needs the output dir to exist
        Directory.CreateDirectory(outputDirectory);

        var args = $"--headless --norestore --convert-to {loFormat} --outdir \"{outputDirectory}\" \"{inputPath}\"";

        progress?.Report(20);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _sofficePath,
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

        // Wait with timeout
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(5));

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { }
            throw new TimeoutException("LibreOffice conversion timed out after 5 minutes.");
        }

        progress?.Report(80);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"LibreOffice conversion failed (exit code {process.ExitCode}): {error}");
        }

        // Find the output file — LibreOffice names it based on input filename + target extension
        var expectedName = Path.GetFileNameWithoutExtension(inputPath) + "." + loFormat;
        var outputPath = Path.Combine(outputDirectory, expectedName);

        if (!File.Exists(outputPath))
        {
            // Sometimes LibreOffice uses slightly different naming, search for it
            var candidates = Directory.GetFiles(outputDirectory, $"*.{loFormat}");
            outputPath = candidates.FirstOrDefault()
                ?? throw new FileNotFoundException($"LibreOffice did not produce output file. Expected: {expectedName}");
        }

        progress?.Report(100);
        return outputPath;
    }

    private static string? FindSofficePath()
    {
        // Windows paths
        string[] windowsPaths =
        [
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            @"C:\Program Files (x86)\LibreOffice\program\soffice.exe",
        ];

        foreach (var path in windowsPaths)
        {
            if (File.Exists(path)) return path;
        }

        // Linux/macOS — check PATH
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

        // macOS specific
        if (File.Exists("/Applications/LibreOffice.app/Contents/MacOS/soffice"))
            return "/Applications/LibreOffice.app/Contents/MacOS/soffice";

        return null;
    }

    public static bool IsAvailable() => FindSofficePath() != null;
}
