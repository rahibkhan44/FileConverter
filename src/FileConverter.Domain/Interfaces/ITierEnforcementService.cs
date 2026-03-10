using System.Security.Claims;

namespace FileConverter.Domain.Interfaces;

public interface ITierEnforcementService
{
    /// <summary>
    /// Validates whether a conversion request is allowed based on the user's tier limits.
    /// </summary>
    /// <param name="user">The authenticated user's claims, or null for anonymous requests.</param>
    /// <param name="ipAddress">The requester's IP address (used for anonymous rate limiting).</param>
    /// <param name="fileSize">The size of the file in bytes.</param>
    /// <param name="sourceFormat">The source file format extension (e.g., "mp4", "png").</param>
    /// <param name="targetFormat">The target file format extension (e.g., "mp3", "jpg").</param>
    /// <returns>A tuple indicating whether the request is allowed and an optional reason if blocked.</returns>
    Task<(bool Allowed, string? Reason)> ValidateConversionRequestAsync(
        ClaimsPrincipal? user,
        string ipAddress,
        long fileSize,
        string sourceFormat,
        string targetFormat);
}
