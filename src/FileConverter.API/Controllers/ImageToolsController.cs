using FileConverter.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace FileConverter.API.Controllers;

[ApiController]
[Route("api/v1/image")]
public class ImageToolsController : ControllerBase
{
    private readonly ImageToolsService _imageTools;

    public ImageToolsController(ImageToolsService imageTools)
    {
        _imageTools = imageTools;
    }

    /// <summary>
    /// Compresses an image by reducing quality.
    /// </summary>
    [HttpPost("compress")]
    [RequestSizeLimit(524_288_000)]
    public async Task<IActionResult> Compress([FromForm] IFormFile file, [FromForm] int quality = 75)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        var (inputPath, outputPath, tempDir) = await SaveToTemp(file, "compressed_");

        try
        {
            _imageTools.Compress(inputPath, outputPath, quality);
            var bytes = await System.IO.File.ReadAllBytesAsync(outputPath);
            return File(bytes, GetContentType(file.FileName), "compressed_" + file.FileName);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// Resizes an image to the specified dimensions.
    /// </summary>
    [HttpPost("resize")]
    [RequestSizeLimit(524_288_000)]
    public async Task<IActionResult> Resize([FromForm] IFormFile file, [FromForm] int? width = null,
        [FromForm] int? height = null, [FromForm] bool maintainAspectRatio = true)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });
        if (!width.HasValue && !height.HasValue)
            return BadRequest(new { error = "Specify at least width or height." });

        var (inputPath, outputPath, tempDir) = await SaveToTemp(file, "resized_");

        try
        {
            _imageTools.Resize(inputPath, outputPath, width, height, maintainAspectRatio);
            var bytes = await System.IO.File.ReadAllBytesAsync(outputPath);
            return File(bytes, GetContentType(file.FileName), "resized_" + file.FileName);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// Crops an image to the specified rectangle.
    /// </summary>
    [HttpPost("crop")]
    [RequestSizeLimit(524_288_000)]
    public async Task<IActionResult> Crop([FromForm] IFormFile file, [FromForm] int x, [FromForm] int y,
        [FromForm] int width, [FromForm] int height)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        var (inputPath, outputPath, tempDir) = await SaveToTemp(file, "cropped_");

        try
        {
            _imageTools.Crop(inputPath, outputPath, x, y, width, height);
            var bytes = await System.IO.File.ReadAllBytesAsync(outputPath);
            return File(bytes, GetContentType(file.FileName), "cropped_" + file.FileName);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// Rotates an image by the specified degrees.
    /// </summary>
    [HttpPost("rotate")]
    [RequestSizeLimit(524_288_000)]
    public async Task<IActionResult> Rotate([FromForm] IFormFile file, [FromForm] double degrees = 90)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        var (inputPath, outputPath, tempDir) = await SaveToTemp(file, "rotated_");

        try
        {
            _imageTools.Rotate(inputPath, outputPath, degrees);
            var bytes = await System.IO.File.ReadAllBytesAsync(outputPath);
            return File(bytes, GetContentType(file.FileName), "rotated_" + file.FileName);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// Adds a text watermark to the image.
    /// </summary>
    [HttpPost("watermark")]
    [RequestSizeLimit(524_288_000)]
    public async Task<IActionResult> Watermark([FromForm] IFormFile file, [FromForm] string text,
        [FromForm] int fontSize = 24, [FromForm] string color = "#80808080")
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });
        if (string.IsNullOrWhiteSpace(text))
            return BadRequest(new { error = "Watermark text is required." });

        var (inputPath, outputPath, tempDir) = await SaveToTemp(file, "watermarked_");

        try
        {
            _imageTools.AddTextWatermark(inputPath, outputPath, text, fontSize, color);
            var bytes = await System.IO.File.ReadAllBytesAsync(outputPath);
            return File(bytes, GetContentType(file.FileName), "watermarked_" + file.FileName);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// Strips EXIF and other metadata from the image.
    /// </summary>
    [HttpPost("strip-metadata")]
    [RequestSizeLimit(524_288_000)]
    public async Task<IActionResult> StripMetadata([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        var (inputPath, outputPath, tempDir) = await SaveToTemp(file, "stripped_");

        try
        {
            _imageTools.StripMetadata(inputPath, outputPath);
            var bytes = await System.IO.File.ReadAllBytesAsync(outputPath);
            return File(bytes, GetContentType(file.FileName), file.FileName);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// Reads image metadata (dimensions, format, EXIF data).
    /// </summary>
    [HttpPost("metadata")]
    [RequestSizeLimit(524_288_000)]
    public async Task<IActionResult> GetMetadata([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        var tempDir = Path.Combine(Path.GetTempPath(), "fileconverter", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var inputPath = Path.Combine(tempDir, file.FileName);

        try
        {
            await using (var fs = new FileStream(inputPath, FileMode.Create))
                await file.CopyToAsync(fs);

            var metadata = _imageTools.GetMetadata(inputPath);
            return Ok(metadata);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private async Task<(string InputPath, string OutputPath, string TempDir)> SaveToTemp(IFormFile file, string prefix)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "fileconverter", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var inputPath = Path.Combine(tempDir, file.FileName);
        var outputPath = Path.Combine(tempDir, prefix + file.FileName);

        await using var fs = new FileStream(inputPath, FileMode.Create);
        await file.CopyToAsync(fs);

        return (inputPath, outputPath, tempDir);
    }

    private static string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" or ".jfif" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".tiff" or ".tif" => "image/tiff",
            ".svg" => "image/svg+xml",
            ".ico" => "image/x-icon",
            ".avif" => "image/avif",
            _ => "application/octet-stream"
        };
    }
}
