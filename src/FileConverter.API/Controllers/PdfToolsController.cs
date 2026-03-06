using FileConverter.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;

namespace FileConverter.API.Controllers;

[ApiController]
[Route("api/v1/pdf")]
public class PdfToolsController : ControllerBase
{
    private readonly PdfToolsService _pdfTools;

    public PdfToolsController(PdfToolsService pdfTools)
    {
        _pdfTools = pdfTools;
    }

    /// <summary>
    /// Merges multiple PDF files into one.
    /// </summary>
    [HttpPost("merge")]
    [RequestSizeLimit(524_288_000)] // 500MB
    public async Task<IActionResult> Merge([FromForm] List<IFormFile> files)
    {
        if (files == null || files.Count < 2)
            return BadRequest(new { error = "At least 2 PDF files are required." });

        var tempDir = Path.Combine(Path.GetTempPath(), "fileconverter", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var inputPaths = new List<string>();
            foreach (var file in files)
            {
                var path = Path.Combine(tempDir, file.FileName);
                await using var fs = new FileStream(path, FileMode.Create);
                await file.CopyToAsync(fs);
                inputPaths.Add(path);
            }

            var outputPath = Path.Combine(tempDir, "merged.pdf");
            _pdfTools.Merge(inputPaths, outputPath);

            var bytes = await System.IO.File.ReadAllBytesAsync(outputPath);
            return File(bytes, "application/pdf", "merged.pdf");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// Splits a PDF into separate files by page ranges.
    /// </summary>
    [HttpPost("split")]
    [RequestSizeLimit(524_288_000)]
    public async Task<IActionResult> Split([FromForm] IFormFile file, [FromForm] string? pages = null)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No PDF file provided." });

        var tempDir = Path.Combine(Path.GetTempPath(), "fileconverter", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var inputPath = Path.Combine(tempDir, file.FileName);
            await using (var fs = new FileStream(inputPath, FileMode.Create))
                await file.CopyToAsync(fs);

            var outputDir = Path.Combine(tempDir, "output");
            List<string> resultPaths;

            if (string.IsNullOrEmpty(pages))
            {
                // Split into individual pages
                resultPaths = _pdfTools.SplitAllPages(inputPath, outputDir);
            }
            else
            {
                // Parse page ranges like "1-3,5-7,10"
                var ranges = ParsePageRanges(pages);
                resultPaths = _pdfTools.Split(inputPath, outputDir, ranges);
            }

            if (resultPaths.Count == 1)
            {
                var bytes = await System.IO.File.ReadAllBytesAsync(resultPaths[0]);
                return File(bytes, "application/pdf", Path.GetFileName(resultPaths[0]));
            }

            // Multiple files — return as ZIP
            var zipPath = Path.Combine(tempDir, "split.zip");
            using (var zipStream = new FileStream(zipPath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                foreach (var path in resultPaths)
                {
                    archive.CreateEntryFromFile(path, Path.GetFileName(path));
                }
            }

            var zipBytes = await System.IO.File.ReadAllBytesAsync(zipPath);
            return File(zipBytes, "application/zip", "split_pages.zip");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// Gets the page count of a PDF.
    /// </summary>
    [HttpPost("page-count")]
    [RequestSizeLimit(524_288_000)]
    public async Task<IActionResult> GetPageCount([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No PDF file provided." });

        var tempPath = Path.Combine(Path.GetTempPath(), "fileconverter", Guid.NewGuid().ToString() + ".pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);

        try
        {
            await using (var fs = new FileStream(tempPath, FileMode.Create))
                await file.CopyToAsync(fs);

            var count = _pdfTools.GetPageCount(tempPath);
            return Ok(new { pageCount = count, fileName = file.FileName });
        }
        finally
        {
            try { System.IO.File.Delete(tempPath); } catch { }
        }
    }

    private static List<(int Start, int End)> ParsePageRanges(string pages)
    {
        var ranges = new List<(int, int)>();
        foreach (var part in pages.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var dashParts = part.Split('-');
            if (dashParts.Length == 2 && int.TryParse(dashParts[0], out var start) && int.TryParse(dashParts[1], out var end))
            {
                ranges.Add((start, end));
            }
            else if (int.TryParse(part, out var single))
            {
                ranges.Add((single, single));
            }
        }
        return ranges;
    }
}
