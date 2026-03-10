using FileConverter.Application.DTOs;
using FileConverter.Domain.Enums;
using FileConverter.Domain.Interfaces;
using FileConverter.Domain.Models;

namespace FileConverter.Application.Services;

public class ConversionService
{
    private readonly IFileStorageService _storage;
    private readonly IJobTracker _jobTracker;
    private readonly IRateLimitService _rateLimit;
    private readonly IJobQueue _jobQueue;

    private const long MaxFileSizeBytes = 500 * 1024 * 1024; // 500MB

    public ConversionService(IFileStorageService storage, IJobTracker jobTracker,
        IRateLimitService rateLimit, IJobQueue jobQueue)
    {
        _storage = storage;
        _jobTracker = jobTracker;
        _rateLimit = rateLimit;
        _jobQueue = jobQueue;
    }

    public async Task<ConvertResponse> CreateJobAsync(Stream fileStream, string fileName, string targetFormatStr,
        Dictionary<string, string>? options, string ipAddress, CancellationToken cancellationToken, string? callbackUrl = null)
    {
        if (!_rateLimit.IsAllowed(ipAddress))
            throw new InvalidOperationException("Daily conversion limit reached. Try again tomorrow.");

        var sourceFormat = SupportedConversions.ParseFormat(Path.GetExtension(fileName))
            ?? throw new ArgumentException($"Unsupported file format: {Path.GetExtension(fileName)}");

        var targetFormat = SupportedConversions.ParseFormat(targetFormatStr)
            ?? throw new ArgumentException($"Unsupported target format: {targetFormatStr}");

        if (!SupportedConversions.IsSupported(sourceFormat, targetFormat))
            throw new ArgumentException($"Cannot convert {sourceFormat} to {targetFormat}");

        var savedPath = await _storage.SaveUploadedFileAsync(fileStream, fileName, cancellationToken);

        if (_storage.GetFileSize(savedPath) > MaxFileSizeBytes)
        {
            _storage.DeleteFile(savedPath);
            throw new ArgumentException("File exceeds maximum size of 500MB.");
        }

        var job = new ConversionJob
        {
            OriginalFileName = fileName,
            InputFilePath = savedPath,
            SourceFormat = sourceFormat,
            TargetFormat = targetFormat,
            Options = options ?? new(),
            CallbackUrl = callbackUrl
        };

        _jobTracker.AddJob(job);
        _rateLimit.RecordConversion(ipAddress);

        await _jobQueue.EnqueueAsync(job.Id, cancellationToken);

        return new ConvertResponse
        {
            JobId = job.Id,
            FileName = fileName,
            SourceFormat = sourceFormat.ToString().ToLower(),
            TargetFormat = targetFormat.ToString().ToLower()
        };
    }

    public async Task<BatchConvertResponse> CreateBatchJobAsync(IList<(Stream Stream, string FileName)> files,
        string targetFormatStr, Dictionary<string, string>? options, string ipAddress, CancellationToken cancellationToken)
    {
        if (files.Count > 100)
            throw new ArgumentException("Maximum 100 files per batch.");

        if (!_rateLimit.IsAllowed(ipAddress))
            throw new InvalidOperationException("Daily conversion limit reached. Try again tomorrow.");

        var targetFormat = SupportedConversions.ParseFormat(targetFormatStr)
            ?? throw new ArgumentException($"Unsupported target format: {targetFormatStr}");

        var batch = new BatchConversionJob { TargetFormat = targetFormat };
        var responses = new List<ConvertResponse>();

        foreach (var (stream, fileName) in files)
        {
            var sourceFormat = SupportedConversions.ParseFormat(Path.GetExtension(fileName));
            if (sourceFormat == null || !SupportedConversions.IsSupported(sourceFormat.Value, targetFormat))
                continue;

            var savedPath = await _storage.SaveUploadedFileAsync(stream, fileName, cancellationToken);

            if (_storage.GetFileSize(savedPath) > MaxFileSizeBytes)
            {
                _storage.DeleteFile(savedPath);
                continue;
            }

            var job = new ConversionJob
            {
                OriginalFileName = fileName,
                InputFilePath = savedPath,
                SourceFormat = sourceFormat.Value,
                TargetFormat = targetFormat,
                Options = options ?? new()
            };

            batch.Jobs.Add(job);
            _jobTracker.AddJob(job);

            responses.Add(new ConvertResponse
            {
                JobId = job.Id,
                FileName = fileName,
                SourceFormat = sourceFormat.Value.ToString().ToLower(),
                TargetFormat = targetFormat.ToString().ToLower()
            });
        }

        if (batch.Jobs.Count == 0)
            throw new ArgumentException("No valid files to convert.");

        _rateLimit.RecordConversion(ipAddress);
        _jobTracker.AddBatchJob(batch);

        // Enqueue all batch jobs
        foreach (var job in batch.Jobs)
            await _jobQueue.EnqueueAsync(job.Id, cancellationToken);

        return new BatchConvertResponse
        {
            BatchId = batch.Id,
            FileCount = batch.Jobs.Count,
            TargetFormat = targetFormat.ToString().ToLower(),
            Jobs = responses
        };
    }

    public JobStatusResponse? GetJobStatus(Guid jobId)
    {
        var job = _jobTracker.GetJob(jobId);
        if (job == null) return null;

        return new JobStatusResponse
        {
            JobId = job.Id,
            FileName = job.OriginalFileName,
            Status = job.Status.ToString(),
            Progress = job.Progress,
            ErrorMessage = job.ErrorMessage,
            SourceFormat = job.SourceFormat.ToString().ToLower(),
            TargetFormat = job.TargetFormat.ToString().ToLower()
        };
    }

    public BatchStatusResponse? GetBatchStatus(Guid batchId)
    {
        var batch = _jobTracker.GetBatchJob(batchId);
        if (batch == null) return null;

        return new BatchStatusResponse
        {
            BatchId = batch.Id,
            Status = batch.Status.ToString(),
            OverallProgress = batch.OverallProgress,
            Files = batch.Jobs.Select(j => new JobStatusResponse
            {
                JobId = j.Id,
                FileName = j.OriginalFileName,
                Status = j.Status.ToString(),
                Progress = j.Progress,
                ErrorMessage = j.ErrorMessage,
                SourceFormat = j.SourceFormat.ToString().ToLower(),
                TargetFormat = j.TargetFormat.ToString().ToLower()
            }).ToList()
        };
    }
}
