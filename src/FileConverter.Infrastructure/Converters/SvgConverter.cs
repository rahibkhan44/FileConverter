using FileConverter.Application;
using FileConverter.Domain.Enums;
using FileConverter.Domain.Interfaces;
using SixLabors.ImageSharp;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using Svg.Skia;

namespace FileConverter.Infrastructure.Converters;

public class SvgConverter : IFileConverter
{
    private static readonly HashSet<FileFormat> SupportedTargets = new()
    {
        FileFormat.Png, FileFormat.Jpg, FileFormat.WebP, FileFormat.Bmp, FileFormat.Tiff, FileFormat.Gif, FileFormat.Pdf
    };

    public bool CanConvert(FileFormat source, FileFormat target)
        => source == FileFormat.Svg && SupportedTargets.Contains(target);

    public async Task<string> ConvertAsync(string inputPath, string outputDirectory, FileFormat targetFormat,
        Dictionary<string, string> options, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(10);

        var svgText = await File.ReadAllTextAsync(inputPath, cancellationToken);

        using var svg = new SKSvg();
        svg.FromSvg(svgText);

        if (svg.Picture == null)
            throw new InvalidOperationException("Failed to parse SVG file.");

        progress?.Report(30);

        int width = options.TryGetValue("width", out var w) && int.TryParse(w, out var wv) ? wv : (int)svg.Picture.CullRect.Width;
        int height = options.TryGetValue("height", out var h) && int.TryParse(h, out var hv) ? hv : (int)svg.Picture.CullRect.Height;

        if (width <= 0) width = 800;
        if (height <= 0) height = 600;

        using var bitmap = svg.Picture.ToBitmap(
            SkiaSharp.SKColors.Transparent, 1f, 1f,
            SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul,
            SkiaSharp.SKColorSpace.CreateSrgb());

        if (bitmap == null)
            throw new InvalidOperationException("Failed to render SVG to bitmap.");

        progress?.Report(50);

        // Convert SkiaSharp bitmap to ImageSharp Image
        using var pixelData = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        using var stream = pixelData.AsStream();
        using var image = await SixLabors.ImageSharp.Image.LoadAsync(stream, cancellationToken);

        // Resize if needed
        if (image.Width != width || image.Height != height)
        {
            bool maintainAspect = !options.TryGetValue("maintainAspectRatio", out var ar) || !bool.TryParse(ar, out var arv) || arv;
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new SixLabors.ImageSharp.Size(width, height),
                Mode = maintainAspect ? ResizeMode.Max : ResizeMode.Stretch
            }));
        }

        progress?.Report(70);

        var outputFileName = Path.GetFileNameWithoutExtension(inputPath) + SupportedConversions.GetExtension(targetFormat);
        var outputPath = Path.Combine(outputDirectory, outputFileName);

        int quality = options.TryGetValue("quality", out var q) && int.TryParse(q, out var qv) ? qv : 85;

        if (targetFormat == FileFormat.Pdf)
        {
            // SVG → PDF: render to PNG first, then embed in PDF
            using var pngStream = new MemoryStream();
            await image.SaveAsync(pngStream, new PngEncoder(), cancellationToken);
            var pngBytes = pngStream.ToArray();

            var pageSizeStr = options.TryGetValue("pageSize", out var ps) ? ps : "A4";
            QuestPDF.Helpers.PageSize pageSize = pageSizeStr.ToUpperInvariant() switch
            {
                "A3" => PageSizes.A3,
                "A5" => PageSizes.A5,
                "LETTER" => PageSizes.Letter,
                _ => PageSizes.A4
            };

            float margin = options.TryGetValue("marginMm", out var m2) && float.TryParse(m2, out var mv) ? mv : 10;

            QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(pageSize);
                    page.Margin(margin, Unit.Millimetre);
                    page.Content().Image(pngBytes).FitArea();
                });
            }).GeneratePdf(outputPath);
        }
        else
        {
            var encoder = targetFormat switch
            {
                FileFormat.Png => (SixLabors.ImageSharp.Formats.IImageEncoder)new PngEncoder(),
                FileFormat.Jpg => new JpegEncoder { Quality = quality },
                FileFormat.WebP => new WebpEncoder { Quality = quality },
                FileFormat.Bmp => new BmpEncoder(),
                FileFormat.Tiff => new TiffEncoder(),
                FileFormat.Gif => new GifEncoder(),
                _ => throw new NotSupportedException($"SVG cannot be converted to {targetFormat}")
            };

            await image.SaveAsync(outputPath, encoder, cancellationToken);
        }

        progress?.Report(100);

        return outputPath;
    }
}
