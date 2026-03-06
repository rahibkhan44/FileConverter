using FileConverter.Shared.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace FileConverter.Shared;

public class FileConverterApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public FileConverterApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // Single file conversion
    public async Task<ConvertResponse> ConvertFileAsync(Stream fileStream, string fileName, string targetFormat,
        Dictionary<string, string>? options = null, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", fileName);
        content.Add(new StringContent(targetFormat), "targetFormat");

        if (options != null)
            content.Add(new StringContent(JsonSerializer.Serialize(options)), "options");

        var response = await _httpClient.PostAsync("api/v1/convert", content, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<ConvertResponse>(JsonOptions, cancellationToken))!;
    }

    // Batch conversion
    public async Task<BatchConvertResponse> ConvertBatchAsync(IEnumerable<(Stream Stream, string FileName)> files,
        string targetFormat, Dictionary<string, string>? options = null, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        foreach (var (stream, fileName) in files)
        {
            content.Add(new StreamContent(stream), "files", fileName);
        }
        content.Add(new StringContent(targetFormat), "targetFormat");

        if (options != null)
            content.Add(new StringContent(JsonSerializer.Serialize(options)), "options");

        var response = await _httpClient.PostAsync("api/v1/convert/batch", content, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<BatchConvertResponse>(JsonOptions, cancellationToken))!;
    }

    // Poll job status
    public async Task<JobStatusResponse> GetJobStatusAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/v1/convert/{jobId}/status", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<JobStatusResponse>(JsonOptions, cancellationToken))!;
    }

    // Poll batch status
    public async Task<BatchStatusResponse> GetBatchStatusAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/v1/convert/batch/{batchId}/status", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<BatchStatusResponse>(JsonOptions, cancellationToken))!;
    }

    // Download converted file
    public async Task<(Stream Stream, string FileName)> DownloadFileAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/v1/convert/{jobId}/download", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? "converted_file";

        return (stream, fileName);
    }

    // Download batch as ZIP
    public async Task<Stream> DownloadBatchZipAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/v1/convert/batch/{batchId}/download", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    // Download individual batch file
    public async Task<(Stream Stream, string FileName)> DownloadBatchFileAsync(Guid batchId, Guid fileId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/v1/convert/batch/{batchId}/files/{fileId}/download", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? "converted_file";

        return (stream, fileName);
    }

    // Get supported formats
    public async Task<List<FormatInfoResponse>> GetFormatsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/v1/formats", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<List<FormatInfoResponse>>(JsonOptions, cancellationToken))!;
    }

    // Get format options
    public async Task<List<FormatOptionInfo>> GetFormatOptionsAsync(string sourceFormat, string targetFormat, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/v1/formats/{sourceFormat}/options?target={targetFormat}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<List<FormatOptionInfo>>(JsonOptions, cancellationToken))!;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            try
            {
                var apiError = JsonSerializer.Deserialize<ApiError>(errorContent, JsonOptions);
                throw new HttpRequestException(apiError?.Error ?? errorContent, null, response.StatusCode);
            }
            catch (JsonException)
            {
                throw new HttpRequestException(errorContent, null, response.StatusCode);
            }
        }
    }
}
