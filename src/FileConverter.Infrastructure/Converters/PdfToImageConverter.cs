using FileConverter.Application;
using FileConverter.Domain.Enums;
using FileConverter.Domain.Interfaces;
using PDFtoImage;

namespace FileConverter.Infrastructure.Converters;

/// <summary>
/// Converts PDF pages to images (PNG/JPG) using the PDFtoImage library (bundles PDFium).
/// WARNING: PDFium is NOT thread-safe — this converter uses a static SemaphoreSlim(1).
/// </summary>
public class PdfToImageConverter : IFileConverter
{
    private static readonly SemaphoreSlim PdfiumLock = new(1, 1);

    private static readonly HashSet<FileFormat> SupportedTargets = new()
    {
        FileFormat.Png, FileFormat.Jpg
    };

    public bool CanConvert(FileFormat source, FileFormat target)
        => source == FileFormat.Pdf && SupportedTargets.Contains(target);

    public async Task<string> ConvertAsync(string inputPath, string outputDirectory, FileFormat targetFormat,
        Dictionary<string, string> options, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(5);

        var ext = targetFormat == FileFormat.Jpg ? ".jpg" : ".png";
        var outputFileName = Path.GetFileNameWithoutExtension(inputPath) + ext;
        var outputPath = Path.Combine(outputDirectory, outputFileName);
        Directory.CreateDirectory(outputDirectory);

        int dpi = options.TryGetValue("dpi", out var d) && int.TryParse(d, out var dv) ? dv : 300;
        int? pageIndex = options.TryGetValue("page", out var p) && int.TryParse(p, out var pv) ? pv : null;

        progress?.Report(10);

        // PDFium is not thread-safe — acquire lock
        await PdfiumLock.WaitAsync(cancellationToken);
        try
        {
            using var pdfStream = File.OpenRead(inputPath);
            int page = pageIndex ?? 0;
            var renderOptions = new RenderOptions(Dpi: dpi);

            progress?.Report(30);

            if (targetFormat == FileFormat.Jpg)
                Conversion.SaveJpeg(outputPath, pdfStream, page, leaveOpen: true, password: null, options: renderOptions);
            else
                Conversion.SavePng(outputPath, pdfStream, page, leaveOpen: true, password: null, options: renderOptions);

            progress?.Report(100);
        }
        finally
        {
            PdfiumLock.Release();
        }

        return outputPath;
    }
}
