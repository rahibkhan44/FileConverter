using FileConverter.Application;
using FileConverter.Domain.Enums;
using FileConverter.Domain.Interfaces;
using ImageMagick;

namespace FileConverter.Infrastructure.Converters;

/// <summary>
/// Primary image converter using Magick.NET (self-contained, Apache 2.0).
/// Handles 30+ image formats including HEIC (read-only), AVIF, PSD, RAW camera, etc.
/// Replaces ImageSharp as the primary image engine for raster-to-raster conversions.
/// </summary>
public class MagickImageConverter : IFileConverter
{
    // All formats Magick.NET can read
    private static readonly HashSet<FileFormat> ReadableFormats = new()
    {
        FileFormat.Png, FileFormat.Jpg, FileFormat.WebP, FileFormat.Gif,
        FileFormat.Bmp, FileFormat.Tiff, FileFormat.Ico, FileFormat.Heic,
        FileFormat.Avif, FileFormat.Psd, FileFormat.Tga, FileFormat.Jp2,
        FileFormat.Jfif, FileFormat.Dds,
        FileFormat.Dng, FileFormat.Cr2, FileFormat.Nef, FileFormat.Arw
    };

    // Formats Magick.NET can write (HEIC excluded — patent restriction)
    private static readonly HashSet<FileFormat> WritableFormats = new()
    {
        FileFormat.Png, FileFormat.Jpg, FileFormat.WebP, FileFormat.Gif,
        FileFormat.Bmp, FileFormat.Tiff, FileFormat.Ico, FileFormat.Avif,
        FileFormat.Tga, FileFormat.Jp2, FileFormat.Jfif, FileFormat.Dds
    };

    public bool CanConvert(FileFormat source, FileFormat target)
        => ReadableFormats.Contains(source) && WritableFormats.Contains(target) && source != target;

    public async Task<string> ConvertAsync(string inputPath, string outputDirectory, FileFormat targetFormat,
        Dictionary<string, string> options, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(5);

        var outputFileName = Path.GetFileNameWithoutExtension(inputPath) + SupportedConversions.GetExtension(targetFormat);
        var outputPath = Path.Combine(outputDirectory, outputFileName);
        Directory.CreateDirectory(outputDirectory);

        progress?.Report(10);

        using var image = new MagickImage(inputPath);

        progress?.Report(30);

        // Apply options
        ApplyOptions(image, options, targetFormat);

        progress?.Report(50);

        // Set output format
        image.Format = GetMagickFormat(targetFormat);

        // Set quality if applicable
        if (options.TryGetValue("quality", out var q) && int.TryParse(q, out var quality))
            image.Quality = (uint)Math.Clamp(quality, 1, 100);

        progress?.Report(70);

        // Handle ICO specially (needs specific size constraints)
        if (targetFormat == FileFormat.Ico)
        {
            int icoSize = options.TryGetValue("width", out var ws) && int.TryParse(ws, out var wsv) ? Math.Min(wsv, 256) : 256;
            image.Resize((uint)icoSize, (uint)icoSize);
        }

        await Task.Run(() => image.Write(outputPath), cancellationToken);

        progress?.Report(100);
        return outputPath;
    }

    private static void ApplyOptions(MagickImage image, Dictionary<string, string> options, FileFormat targetFormat)
    {
        // Resize
        int? width = options.TryGetValue("width", out var w) && int.TryParse(w, out var wv) ? wv : null;
        int? height = options.TryGetValue("height", out var h) && int.TryParse(h, out var hv) ? hv : null;
        bool maintainAspect = !options.TryGetValue("maintainAspectRatio", out var ar) || !bool.TryParse(ar, out var arv) || arv;

        if (width.HasValue || height.HasValue)
        {
            var geometry = new MagickGeometry(
                (uint)(width ?? 0),
                (uint)(height ?? 0))
            {
                IgnoreAspectRatio = !maintainAspect
            };
            image.Resize(geometry);
        }

        // DPI
        if (options.TryGetValue("dpi", out var d) && int.TryParse(d, out var dpi))
        {
            image.Density = new Density(dpi, dpi, DensityUnit.PixelsPerInch);
        }

        // Strip metadata
        if (options.TryGetValue("stripMetadata", out var sm) && bool.TryParse(sm, out var strip) && strip)
        {
            image.Strip();
        }

        // Background color (useful for formats that don't support transparency)
        if (options.TryGetValue("backgroundColor", out var bg) && !string.IsNullOrEmpty(bg))
        {
            image.BackgroundColor = new MagickColor(bg);
            if (targetFormat is FileFormat.Jpg or FileFormat.Jfif or FileFormat.Bmp)
            {
                image.Alpha(AlphaOption.Remove);
            }
        }
        else if (targetFormat is FileFormat.Jpg or FileFormat.Jfif or FileFormat.Bmp)
        {
            // Auto-flatten transparency for formats that don't support it
            image.BackgroundColor = MagickColors.White;
            image.Alpha(AlphaOption.Remove);
        }
    }

    private static MagickFormat GetMagickFormat(FileFormat format) => format switch
    {
        FileFormat.Png => MagickFormat.Png,
        FileFormat.Jpg => MagickFormat.Jpeg,
        FileFormat.WebP => MagickFormat.WebP,
        FileFormat.Gif => MagickFormat.Gif,
        FileFormat.Bmp => MagickFormat.Bmp,
        FileFormat.Tiff => MagickFormat.Tiff,
        FileFormat.Ico => MagickFormat.Ico,
        FileFormat.Avif => MagickFormat.Avif,
        FileFormat.Tga => MagickFormat.Tga,
        FileFormat.Jp2 => MagickFormat.Jp2,
        FileFormat.Jfif => MagickFormat.Jpeg,
        FileFormat.Dds => MagickFormat.Dds,
        _ => throw new NotSupportedException($"Cannot write format {format}")
    };
}
