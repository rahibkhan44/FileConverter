using FileConverter.Application;
using FileConverter.Domain.Enums;
using FileConverter.Domain.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace FileConverter.Infrastructure.Converters;

public class ImageToPdfConverter : IFileConverter
{
    private static readonly HashSet<FileFormat> ImageFormats = new()
    {
        FileFormat.Png, FileFormat.Jpg, FileFormat.WebP,
        FileFormat.Gif, FileFormat.Bmp, FileFormat.Tiff, FileFormat.Ico
    };

    public bool CanConvert(FileFormat source, FileFormat target)
        => ImageFormats.Contains(source) && target == FileFormat.Pdf;

    public async Task<string> ConvertAsync(string inputPath, string outputDirectory, FileFormat targetFormat,
        Dictionary<string, string> options, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(10);

        var outputFileName = Path.GetFileNameWithoutExtension(inputPath) + ".pdf";
        var outputPath = Path.Combine(outputDirectory, outputFileName);

        var pageSizeStr = options.TryGetValue("pageSize", out var ps) ? ps : "A4";
        QuestPDF.Helpers.PageSize pageSize = pageSizeStr.ToUpperInvariant() switch
        {
            "A3" => PageSizes.A3,
            "A5" => PageSizes.A5,
            "LETTER" => PageSizes.Letter,
            "LEGAL" => PageSizes.Legal,
            _ => PageSizes.A4
        };

        bool landscape = options.TryGetValue("orientation", out var o) && o.Equals("Landscape", StringComparison.OrdinalIgnoreCase);
        if (landscape) pageSize = new QuestPDF.Helpers.PageSize(pageSize.Height, pageSize.Width);

        float margin = options.TryGetValue("marginMm", out var m) && float.TryParse(m, out var mv) ? mv : 10;

        progress?.Report(30);

        // Convert any image format to PNG bytes first (QuestPDF handles PNG reliably)
        byte[] pngBytes;
        using (var image = await SixLabors.ImageSharp.Image.LoadAsync(inputPath, cancellationToken))
        using (var ms = new MemoryStream())
        {
            await image.SaveAsync(ms, new PngEncoder(), cancellationToken);
            pngBytes = ms.ToArray();
        }

        progress?.Report(60);

        QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(pageSize);
                page.Margin(margin, Unit.Millimetre);
                page.Content().Image(pngBytes).FitArea();
            });
        }).GeneratePdf(outputPath);

        progress?.Report(100);
        return outputPath;
    }
}
