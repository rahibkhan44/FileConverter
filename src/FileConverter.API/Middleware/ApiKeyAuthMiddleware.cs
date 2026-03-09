using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FileConverter.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FileConverter.API.Middleware;

public class ApiKeyAuthMiddleware
{
    private const string ApiKeyHeader = "X-Api-Key";
    private readonly RequestDelegate _next;

    public ApiKeyAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip if already authenticated (JWT)
        if (context.User.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        if (context.Request.Headers.TryGetValue(ApiKeyHeader, out var apiKeyValue))
        {
            var rawKey = apiKeyValue.ToString();
            if (!string.IsNullOrEmpty(rawKey))
            {
                var keyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))).ToLower();

                var db = context.RequestServices.GetRequiredService<AppDbContext>();
                var apiKey = await db.ApiKeys
                    .FirstOrDefaultAsync(k => k.KeyHash == keyHash && !k.IsRevoked);

                if (apiKey != null && (apiKey.ExpiresAt == null || apiKey.ExpiresAt > DateTime.UtcNow))
                {
                    apiKey.LastUsedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();

                    var claims = new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, apiKey.UserId),
                        new Claim("auth_method", "api_key")
                    };

                    context.User = new ClaimsPrincipal(
                        new ClaimsIdentity(claims, "ApiKey"));
                }
            }
        }

        await _next(context);
    }
}
