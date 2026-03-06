using FileConverter.Application.Services;
using FileConverter.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;

namespace FileConverter.API.Controllers;

[ApiController]
[Route("api/v1/convert")]
public class ConvertController : ControllerBase
{
    private readonly ConversionService _conversionService;
    private readonly IJobTracker _jobTracker;
    private readonly IFileStorageService _storage;

    public ConvertController(ConversionService conversionService, IJobTracker jobTracker, IFileStorageService storage)
    {
        _conversionService = conversionService;
        _jobTracker = jobTracker;
        _storage = storage;
    }

    [HttpPost]
    [RequestSizeLimit(52_428_800)] // 50MB
    public async Task<IActionResult> Convert([FromForm] IFormFile file, [FromForm] string targetFormat,
        [FromForm] string? options = null, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        var optionsDict = ParseOptions(options);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        try
        {
            using var stream = file.OpenReadStream();
            var result = await _conversionService.CreateJobAsync(stream, file.FileName, targetFormat, optionsDict, ip, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(429, new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id}/status")]
    public IActionResult GetStatus(Guid id)
    {
        var status = _conversionService.GetJobStatus(id);
        if (status == null) return NotFound(new { error = "Job not found." });
        return Ok(status);
    }

    [HttpGet("{id}/download")]
    public IActionResult Download(Guid id)
    {
        var job = _jobTracker.GetJob(id);
        if (job == null) return NotFound(new { error = "Job not found." });
        if (job.Status != Domain.Enums.ConversionStatus.Completed || job.OutputFilePath == null)
            return BadRequest(new { error = "Job not completed yet." });

        if (!_storage.FileExists(job.OutputFilePath))
            return NotFound(new { error = "File expired or not found." });

        var stream = _storage.OpenRead(job.OutputFilePath);
        var fileName = Path.GetFileName(job.OutputFilePath);
        return File(stream, "application/octet-stream", fileName);
    }

    [HttpPost("batch")]
    [RequestSizeLimit(524_288_000)] // 500MB total for batch
    public async Task<IActionResult> BatchConvert([FromForm] IFormFileCollection files, [FromForm] string targetFormat,
        [FromForm] string? options = null, CancellationToken cancellationToken = default)
    {
        if (files == null || files.Count == 0)
            return BadRequest(new { error = "No files provided." });

        if (files.Count > 100)
            return BadRequest(new { error = "Maximum 100 files per batch." });

        var optionsDict = ParseOptions(options);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        try
        {
            var fileList = new List<(Stream Stream, string FileName)>();
            foreach (var file in files)
            {
                fileList.Add((file.OpenReadStream(), file.FileName));
            }

            var result = await _conversionService.CreateBatchJobAsync(fileList, targetFormat, optionsDict, ip, cancellationToken);

            // Dispose streams after saving
            foreach (var (stream, _) in fileList)
                await stream.DisposeAsync();

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(429, new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("batch/{id}/status")]
    public IActionResult GetBatchStatus(Guid id)
    {
        var status = _conversionService.GetBatchStatus(id);
        if (status == null) return NotFound(new { error = "Batch not found." });
        return Ok(status);
    }

    [HttpGet("batch/{id}/download")]
    public IActionResult DownloadBatch(Guid id)
    {
        var batch = _jobTracker.GetBatchJob(id);
        if (batch == null) return NotFound(new { error = "Batch not found." });

        var completedJobs = batch.Jobs.Where(j =>
            j.Status == Domain.Enums.ConversionStatus.Completed && j.OutputFilePath != null).ToList();

        if (completedJobs.Count == 0)
            return BadRequest(new { error = "No completed files to download." });

        var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var job in completedJobs)
            {
                if (!_storage.FileExists(job.OutputFilePath!)) continue;
                var entry = archive.CreateEntry(Path.GetFileName(job.OutputFilePath!));
                using var entryStream = entry.Open();
                using var fileStream = _storage.OpenRead(job.OutputFilePath!);
                fileStream.CopyTo(entryStream);
            }
        }

        memoryStream.Position = 0;
        return File(memoryStream, "application/zip", "converted_files.zip");
    }

    [HttpGet("batch/{batchId}/files/{fileId}/download")]
    public IActionResult DownloadBatchFile(Guid batchId, Guid fileId)
    {
        var batch = _jobTracker.GetBatchJob(batchId);
        if (batch == null) return NotFound(new { error = "Batch not found." });

        var job = batch.Jobs.FirstOrDefault(j => j.Id == fileId);
        if (job == null) return NotFound(new { error = "File not found in batch." });

        if (job.Status != Domain.Enums.ConversionStatus.Completed || job.OutputFilePath == null)
            return BadRequest(new { error = "File not completed yet." });

        if (!_storage.FileExists(job.OutputFilePath))
            return NotFound(new { error = "File expired or not found." });

        var stream = _storage.OpenRead(job.OutputFilePath);
        return File(stream, "application/octet-stream", Path.GetFileName(job.OutputFilePath));
    }

    private static Dictionary<string, string>? ParseOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson)) return null;
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(optionsJson);
        }
        catch
        {
            return null;
        }
    }
}
