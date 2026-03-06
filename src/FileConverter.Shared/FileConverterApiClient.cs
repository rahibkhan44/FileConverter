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

    // ═══ PDF Tools ═══

    public async Task<int> GetPdfPageCountAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", fileName);
        var response = await _httpClient.PostAsync("api/v1/pdf/page-count", content, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<PageCountResponse>(JsonOptions, cancellationToken);
        return result?.PageCount ?? 0;
    }

    public async Task<(Stream Stream, string FileName)> MergePdfsAsync(IEnumerable<(Stream Stream, string FileName)> files, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        foreach (var (stream, fileName) in files)
            content.Add(new StreamContent(stream), "files", fileName);
        var response = await _httpClient.PostAsync("api/v1/pdf/merge", content, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadAsStreamAsync(cancellationToken), GetFileName(response, "merged.pdf"));
    }

    public async Task<(Stream Stream, string FileName)> SplitPdfAsync(Stream fileStream, string fileName, string? pages = null, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", fileName);
        if (!string.IsNullOrEmpty(pages))
            content.Add(new StringContent(pages), "pages");
        var response = await _httpClient.PostAsync("api/v1/pdf/split", content, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadAsStreamAsync(cancellationToken), GetFileName(response, "split.pdf"));
    }

    public Task<(Stream Stream, string FileName)> ExtractPdfPagesAsync(Stream fileStream, string fileName, string pages, CancellationToken cancellationToken = default)
    {
        return SplitPdfAsync(fileStream, fileName, pages, cancellationToken);
    }

    // ═══ Image Tools ═══

    public async Task<(Stream Stream, string FileName)> CompressImageAsync(Stream fileStream, string fileName, int quality = 75, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", fileName);
        content.Add(new StringContent(quality.ToString()), "quality");
        var response = await _httpClient.PostAsync("api/v1/image/compress", content, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadAsStreamAsync(cancellationToken), GetFileName(response, fileName));
    }

    public async Task<(Stream Stream, string FileName)> ResizeImageAsync(Stream fileStream, string fileName, int? width, int? height, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", fileName);
        if (width.HasValue) content.Add(new StringContent(width.Value.ToString()), "width");
        if (height.HasValue) content.Add(new StringContent(height.Value.ToString()), "height");
        var response = await _httpClient.PostAsync("api/v1/image/resize", content, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadAsStreamAsync(cancellationToken), GetFileName(response, fileName));
    }

    public async Task<(Stream Stream, string FileName)> CropImageAsync(Stream fileStream, string fileName, int x, int y, int width, int height, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", fileName);
        content.Add(new StringContent(x.ToString()), "x");
        content.Add(new StringContent(y.ToString()), "y");
        content.Add(new StringContent(width.ToString()), "width");
        content.Add(new StringContent(height.ToString()), "height");
        var response = await _httpClient.PostAsync("api/v1/image/crop", content, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadAsStreamAsync(cancellationToken), GetFileName(response, fileName));
    }

    public async Task<(Stream Stream, string FileName)> RotateImageAsync(Stream fileStream, string fileName, double degrees = 90, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", fileName);
        content.Add(new StringContent(degrees.ToString()), "degrees");
        var response = await _httpClient.PostAsync("api/v1/image/rotate", content, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadAsStreamAsync(cancellationToken), GetFileName(response, fileName));
    }

    public async Task<(Stream Stream, string FileName)> WatermarkImageAsync(Stream fileStream, string fileName, string text, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", fileName);
        content.Add(new StringContent(text), "text");
        var response = await _httpClient.PostAsync("api/v1/image/watermark", content, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadAsStreamAsync(cancellationToken), GetFileName(response, fileName));
    }

    public async Task<(Stream Stream, string FileName)> StripMetadataAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", fileName);
        var response = await _httpClient.PostAsync("api/v1/image/strip-metadata", content, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadAsStreamAsync(cancellationToken), GetFileName(response, fileName));
    }

    private static string GetFileName(HttpResponseMessage response, string fallback)
    {
        return response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? fallback;
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
