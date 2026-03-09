using ImageMagick;

namespace FileConverter.Infrastructure.Services;

/// <summary>
/// Image manipulation tools using Magick.NET: compress, resize, crop, rotate, strip metadata.
/// </summary>
public class ImageToolsService
{
    /// <summary>
    /// Compresses an image by reducing quality. Returns output path.
    /// </summary>
    public string Compress(string inputPath, string outputPath, int quality = 75)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var image = new MagickImage(inputPath);
        image.Quality = (uint)Math.Clamp(quality, 1, 100);
        image.Strip(); // Remove metadata to reduce size
        image.Write(outputPath);
        return outputPath;
    }

    /// <summary>
    /// Resizes an image to the specified dimensions.
    /// </summary>
    public string Resize(string inputPath, string outputPath, int? width, int? height, bool maintainAspectRatio = true)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var image = new MagickImage(inputPath);

        var geometry = new MagickGeometry(
            (uint)(width ?? 0),
            (uint)(height ?? 0))
        {
            IgnoreAspectRatio = !maintainAspectRatio
        };
        image.Resize(geometry);
        image.Write(outputPath);
        return outputPath;
    }

    /// <summary>
    /// Crops an image to the specified rectangle.
    /// </summary>
    public string Crop(string inputPath, string outputPath, int x, int y, int width, int height)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var image = new MagickImage(inputPath);
        image.Crop(new MagickGeometry(x, y, (uint)width, (uint)height));
        image.ResetPage();
        image.Write(outputPath);
        return outputPath;
    }

    /// <summary>
    /// Rotates an image by the specified degrees.
    /// </summary>
    public string Rotate(string inputPath, string outputPath, double degrees)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var image = new MagickImage(inputPath);
        image.Rotate(degrees);
        image.Write(outputPath);
        return outputPath;
    }

    /// <summary>
    /// Adds a text watermark to the image.
    /// </summary>
    public string AddTextWatermark(string inputPath, string outputPath, string text, int fontSize = 24, string color = "#80808080")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var image = new MagickImage(inputPath);

        var settings = new MagickReadSettings
        {
            BackgroundColor = MagickColors.Transparent,
            FillColor = new MagickColor(color),
            FontPointsize = fontSize,
            TextGravity = Gravity.Center
        };

        using var watermark = new MagickImage($"caption:{text}", settings);
        watermark.Resize(new MagickGeometry(image.Width, image.Height) { IgnoreAspectRatio = false });

        image.Composite(watermark, Gravity.Center, CompositeOperator.Over);
        image.Write(outputPath);
        return outputPath;
    }

    /// <summary>
    /// Strips EXIF and other metadata from the image.
    /// </summary>
    public string StripMetadata(string inputPath, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var image = new MagickImage(inputPath);
        image.Strip();
        image.Write(outputPath);
        return outputPath;
    }

    /// <summary>
    /// Adjusts image color: brightness, contrast, saturation.
    /// Values are percentages: 100 = no change, >100 = increase, <100 = decrease.
    /// </summary>
    public string AdjustColor(string inputPath, string outputPath, int brightness = 100, int contrast = 100, int saturation = 100)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var image = new MagickImage(inputPath);

        if (brightness != 100)
            image.BrightnessContrast(new Percentage(brightness - 100), new Percentage(0));

        if (contrast != 100)
            image.BrightnessContrast(new Percentage(0), new Percentage(contrast - 100));

        if (saturation != 100)
            image.Modulate(new Percentage(100), new Percentage(saturation), new Percentage(100));

        image.Write(outputPath);
        return outputPath;
    }

    /// <summary>
    /// Reads image metadata (dimensions, format, EXIF data).
    /// </summary>
    public ImageMetadata GetMetadata(string inputPath)
    {
        using var image = new MagickImage(inputPath);
        var profile = image.GetExifProfile();

        return new ImageMetadata
        {
            Width = (int)image.Width,
            Height = (int)image.Height,
            Format = image.Format.ToString(),
            ColorSpace = image.ColorSpace.ToString(),
            Density = image.Density.ToString(),
            FileSize = new FileInfo(inputPath).Length,
            HasAlpha = image.HasAlpha,
            ExifData = profile?.Values
                .Where(v => v.GetValue() != null)
                .ToDictionary(v => v.Tag.ToString(), v => v.GetValue()?.ToString() ?? "")
                ?? new Dictionary<string, string>()
        };
    }

    public class ImageMetadata
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public string Format { get; set; } = "";
        public string ColorSpace { get; set; } = "";
        public string Density { get; set; } = "";
        public long FileSize { get; set; }
        public bool HasAlpha { get; set; }
        public Dictionary<string, string> ExifData { get; set; } = new();
    }
}
