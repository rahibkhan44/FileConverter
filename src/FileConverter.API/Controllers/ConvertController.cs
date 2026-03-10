using FileConverter.Application.Services;
using FileConverter.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using Microsoft.Extensions.Logging;

namespace FileConverter.API.Controllers;

[ApiController]
[Route("api/v1/convert")]
public class ConvertController : ControllerBase
{
    private readonly ConversionService _conversionService;
    private readonly IJobTracker _jobTracker;
    private readonly IFileStorageService _storage;
    private readonly ILogger<ConvertController> _logger;
    private readonly ITierEnforcementService _tierEnforcement;

    public ConvertController(
        ConversionService conversionService,
        IJobTracker jobTracker,
        IFileStorageService storage,
        ILogger<ConvertController> logger,
        ITierEnforcementService tierEnforcement)
    {
        _conversionService = conversionService;
        _jobTracker = jobTracker;
        _storage = storage;
        _logger = logger;
        _tierEnforcement = tierEnforcement;
    }

    [HttpPost]
    [RequestSizeLimit(52_428_800)] // 50MB
    public async Task<IActionResult> Convert([FromForm] IFormFile file, [FromForm] string targetFormat,
        [FromForm] string? options = null, [FromForm] string? callbackUrl = null, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        // Validate file signature (magic bytes) against declared extension
        using var validationStream = file.OpenReadStream();
        var (isValid, detectedMimeType) = MimeValidator.ValidateFileSignature(validationStream, file.FileName, _logger);
        if (!isValid)
        {
            return BadRequest(new { error = $"File type mismatch: the file content does not match the extension '{Path.GetExtension(file.FileName)}'. Detected type: {detectedMimeType ?? "unknown"}." });
        }
        await validationStream.DisposeAsync();

        var optionsDict = ParseOptions(options);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Tier enforcement: check limits before processing
        var sourceExt = Path.GetExtension(file.FileName).TrimStart('.');
        var (tierAllowed, tierReason) = await _tierEnforcement.ValidateConversionRequestAsync(
            User, ip, file.Length, sourceExt, targetFormat);
        if (!tierAllowed)
        {
            return StatusCode(403, new { error = tierReason });
        }

        try
        {
            using var stream = file.OpenReadStream();
            var result = await _conversionService.CreateJobAsync(stream, file.FileName, targetFormat, optionsDict, ip, cancellationToken, callbackUrl);
            return Accepted(result);
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
        var ext = Path.GetExtension(job.OutputFilePath);
        var downloadName = Path.GetFileNameWithoutExtension(job.OriginalFileName) + ext;
        return File(stream, "application/octet-stream", downloadName);
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

        // Validate file signatures for all files in the batch
        foreach (var file in files)
        {
            using var valStream = file.OpenReadStream();
            var (isFileValid, detectedMime) = MimeValidator.ValidateFileSignature(valStream, file.FileName, _logger);
            if (!isFileValid)
            {
                return BadRequest(new { error = $"File type mismatch for '{file.FileName}': the file content does not match the extension '{Path.GetExtension(file.FileName)}'. Detected type: {detectedMime ?? "unknown"}." });
            }
        }

        var optionsDict = ParseOptions(options);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Tier enforcement: check limits for each file in the batch
        foreach (var file in files)
        {
            var batchSourceExt = Path.GetExtension(file.FileName).TrimStart('.');
            var (batchAllowed, batchReason) = await _tierEnforcement.ValidateConversionRequestAsync(
                User, ip, file.Length, batchSourceExt, targetFormat);
            if (!batchAllowed)
            {
                return StatusCode(403, new { error = batchReason, file = file.FileName });
            }
        }

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

            return Accepted(result);
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
                var ext = Path.GetExtension(job.OutputFilePath!);
                var downloadName = Path.GetFileNameWithoutExtension(job.OriginalFileName) + ext;
                var entry = archive.CreateEntry(downloadName);
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
        var batchExt = Path.GetExtension(job.OutputFilePath);
        var batchDownloadName = Path.GetFileNameWithoutExtension(job.OriginalFileName) + batchExt;
        return File(stream, "application/octet-stream", batchDownloadName);
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
