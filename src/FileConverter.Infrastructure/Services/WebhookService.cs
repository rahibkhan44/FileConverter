using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace FileConverter.Infrastructure.Services;

public interface IWebhookService
{
    Task SendWebhookAsync(string callbackUrl, object payload, CancellationToken cancellationToken = default);
}

public class WebhookService : IWebhookService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookService> _logger;

    private static readonly int[] RetryDelaysMs = [1000, 2000, 4000];

    public WebhookService(IHttpClientFactory httpClientFactory, ILogger<WebhookService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task SendWebhookAsync(string callbackUrl, object payload, CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt <= RetryDelaysMs.Length; attempt++)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient("Webhooks");
                client.Timeout = TimeSpan.FromSeconds(10);

                var response = await client.PostAsJsonAsync(callbackUrl, payload, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Webhook delivered to {CallbackUrl} (attempt {Attempt})",
                        callbackUrl, attempt + 1);
                    return;
                }

                _logger.LogWarning("Webhook to {CallbackUrl} returned {StatusCode} (attempt {Attempt})",
                    callbackUrl, (int)response.StatusCode, attempt + 1);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Webhook to {CallbackUrl} failed (attempt {Attempt})",
                    callbackUrl, attempt + 1);
            }

            if (attempt < RetryDelaysMs.Length)
            {
                try
                {
                    await Task.Delay(RetryDelaysMs[attempt], cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Webhook retries cancelled for {CallbackUrl}", callbackUrl);
                    return;
                }
            }
        }

        _logger.LogError("Webhook delivery to {CallbackUrl} failed after {MaxAttempts} attempts",
            callbackUrl, RetryDelaysMs.Length + 1);
    }
}
