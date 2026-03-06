using FileConverter.Application;
using FileConverter.Domain.Enums;
using FileConverter.Domain.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace FileConverter.Infrastructure.Converters;

public class ImageConverter : IFileConverter
{
    private static readonly HashSet<FileFormat> RasterFormats = new()
    {
        FileFormat.Png, FileFormat.Jpg, FileFormat.WebP,
        FileFormat.Gif, FileFormat.Bmp, FileFormat.Tiff, FileFormat.Ico
    };

    public bool CanConvert(FileFormat source, FileFormat target)
        => RasterFormats.Contains(source) && RasterFormats.Contains(target) && source != target
           && !(source == FileFormat.Ico && target == FileFormat.Ico);

    public async Task<string> ConvertAsync(string inputPath, string outputDirectory, FileFormat targetFormat,
        Dictionary<string, string> options, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(10);

        using var image = await Image.LoadAsync(inputPath, cancellationToken);

        progress?.Report(30);

        if (targetFormat == FileFormat.Ico)
        {
            // ICO: resize to 256x256 max (standard ICO size)
            int icoSize = options.TryGetValue("width", out var ws) && int.TryParse(ws, out var wsv) ? wsv : 256;
            if (icoSize > 256) icoSize = 256;
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(icoSize, icoSize),
                Mode = ResizeMode.Max
            }));
        }
        else
        {
            ApplyResizeOptions(image, options);
        }

        progress?.Report(50);

        var outputFileName = Path.GetFileNameWithoutExtension(inputPath) + SupportedConversions.GetExtension(targetFormat);
        var outputPath = Path.Combine(outputDirectory, outputFileName);

        progress?.Report(70);

        if (targetFormat == FileFormat.Ico)
        {
            // Write ICO file with PNG data inside
            using var pngStream = new MemoryStream();
            await image.SaveAsync(pngStream, new PngEncoder(), cancellationToken);
            var pngBytes = pngStream.ToArray();
            await WriteIcoFileAsync(outputPath, pngBytes, image.Width, image.Height, cancellationToken);
        }
        else
        {
            var encoder = GetEncoder(targetFormat, options);
            await image.SaveAsync(outputPath, encoder, cancellationToken);
        }

        progress?.Report(100);

        return outputPath;
    }

    private static async Task WriteIcoFileAsync(string path, byte[] pngData, int width, int height, CancellationToken ct)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        // ICO header: reserved(2) + type=1(2) + count=1(2)
        bw.Write((short)0);
        bw.Write((short)1);
        bw.Write((short)1);

        // ICO directory entry (16 bytes)
        bw.Write((byte)(width >= 256 ? 0 : width));   // width (0 = 256)
        bw.Write((byte)(height >= 256 ? 0 : height));  // height (0 = 256)
        bw.Write((byte)0);    // color palette
        bw.Write((byte)0);    // reserved
        bw.Write((short)1);   // color planes
        bw.Write((short)32);  // bits per pixel
        bw.Write(pngData.Length); // image data size
        bw.Write(22);         // offset to image data (6 header + 16 entry)

        // PNG image data
        await fs.WriteAsync(pngData, ct);
    }

    private static void ApplyResizeOptions(Image image, Dictionary<string, string> options)
    {
        int? width = options.TryGetValue("width", out var w) && int.TryParse(w, out var wv) ? wv : null;
        int? height = options.TryGetValue("height", out var h) && int.TryParse(h, out var hv) ? hv : null;
        bool maintainAspect = !options.TryGetValue("maintainAspectRatio", out var ar) || !bool.TryParse(ar, out var arv) || arv;

        if (width.HasValue || height.HasValue)
        {
            var resizeOptions = new ResizeOptions
            {
                Size = new Size(width ?? 0, height ?? 0),
                Mode = maintainAspect ? ResizeMode.Max : ResizeMode.Stretch
            };
            image.Mutate(x => x.Resize(resizeOptions));
        }
    }

    private static IImageEncoder GetEncoder(FileFormat format, Dictionary<string, string> options)
    {
        int quality = options.TryGetValue("quality", out var q) && int.TryParse(q, out var qv) ? qv : 85;

        return format switch
        {
            FileFormat.Png => new PngEncoder(),
            FileFormat.Jpg => new JpegEncoder { Quality = quality },
            FileFormat.WebP => new WebpEncoder { Quality = quality },
            FileFormat.Gif => new GifEncoder(),
            FileFormat.Bmp => new BmpEncoder(),
            FileFormat.Tiff => new TiffEncoder(),
            _ => throw new NotSupportedException($"No encoder for {format}")
        };
    }
}
