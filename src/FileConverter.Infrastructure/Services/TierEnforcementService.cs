using System.Security.Claims;
using FileConverter.Application;
using FileConverter.Domain.Enums;
using FileConverter.Domain.Interfaces;
using FileConverter.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace FileConverter.Infrastructure.Services;

public class TierEnforcementService : ITierEnforcementService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRateLimitService _rateLimitService;
    private readonly ILogger<TierEnforcementService> _logger;

    // Tier limits
    private const int AnonymousDailyLimit = 50;
    private const long AnonymousMaxFileSize = 100L * 1024 * 1024;       // 100MB
    private const int FreeDailyLimit = 20;
    private const long FreeMaxFileSize = 100L * 1024 * 1024;            // 100MB
    private const int ProDailyLimit = 500;
    private const long ProMaxFileSize = 1L * 1024 * 1024 * 1024;       // 1GB
    private const int BusinessDailyLimit = int.MaxValue;                 // Unlimited
    private const long BusinessMaxFileSize = 5L * 1024 * 1024 * 1024;  // 5GB

    public TierEnforcementService(
        UserManager<ApplicationUser> userManager,
        IRateLimitService rateLimitService,
        ILogger<TierEnforcementService> logger)
    {
        _userManager = userManager;
        _rateLimitService = rateLimitService;
        _logger = logger;
    }

    public async Task<(bool Allowed, string? Reason)> ValidateConversionRequestAsync(
        ClaimsPrincipal? user,
        string ipAddress,
        long fileSize,
        string sourceFormat,
        string targetFormat)
    {
        var isAuthenticated = user?.Identity?.IsAuthenticated == true;

        if (!isAuthenticated)
        {
            return ValidateAnonymousRequest(ipAddress, fileSize, sourceFormat, targetFormat);
        }

        return await ValidateAuthenticatedRequestAsync(user!, fileSize, sourceFormat, targetFormat);
    }

    private (bool Allowed, string? Reason) ValidateAnonymousRequest(
        string ipAddress, long fileSize, string sourceFormat, string targetFormat)
    {
        // Check video/audio restriction for anonymous (Free tier)
        var mediaCheck = CheckVideoAudioRestriction(sourceFormat, targetFormat, "Free");
        if (!mediaCheck.Allowed)
            return mediaCheck;

        // Check file size
        if (fileSize > AnonymousMaxFileSize)
        {
            var maxMb = AnonymousMaxFileSize / (1024 * 1024);
            return (false, $"File size exceeds the {maxMb}MB limit for anonymous users. Please register for a free account or upgrade to Pro for larger files.");
        }

        // Check IP-based daily rate limit
        if (!_rateLimitService.IsAllowed(ipAddress, AnonymousDailyLimit))
        {
            return (false, $"Daily conversion limit of {AnonymousDailyLimit} reached for anonymous users. Please register for a free account or try again tomorrow.");
        }

        return (true, null);
    }

    private async Task<(bool Allowed, string? Reason)> ValidateAuthenticatedRequestAsync(
        ClaimsPrincipal user, long fileSize, string sourceFormat, string targetFormat)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Authenticated user has no NameIdentifier claim");
            return (false, "Unable to identify user. Please log in again.");
        }

        var appUser = await _userManager.FindByIdAsync(userId);
        if (appUser == null)
        {
            _logger.LogWarning("User {UserId} not found in database", userId);
            return (false, "User account not found. Please log in again.");
        }

        var tier = appUser.Tier;
        var (dailyLimit, maxFileSize) = GetTierLimits(tier);

        // Use the user's custom limits if they differ from defaults (allows admin overrides)
        if (appUser.DailyConversionLimit != GetDefaultDailyLimit(tier))
            dailyLimit = appUser.DailyConversionLimit;
        if (appUser.MaxFileSizeBytes != GetDefaultMaxFileSize(tier))
            maxFileSize = appUser.MaxFileSizeBytes;

        // Check video/audio restriction for Free tier
        if (tier == UserTier.Free)
        {
            var mediaCheck = CheckVideoAudioRestriction(sourceFormat, targetFormat, "Free");
            if (!mediaCheck.Allowed)
                return mediaCheck;
        }

        // Check file size
        if (fileSize > maxFileSize)
        {
            var maxDisplay = FormatFileSize(maxFileSize);
            var tierName = tier.ToString();
            return (false, $"File size exceeds the {maxDisplay} limit for {tierName} tier. Please upgrade your plan for larger files.");
        }

        // Check daily conversion limit (Business is unlimited)
        if (tier != UserTier.Business && appUser.TotalConversions >= dailyLimit)
        {
            return (false, $"Daily conversion limit of {dailyLimit} reached for {tier} tier. Please upgrade your plan or try again tomorrow.");
        }

        _logger.LogDebug("Tier enforcement passed for user {UserId} (Tier: {Tier})", userId, tier);
        return (true, null);
    }

    private static (bool Allowed, string? Reason) CheckVideoAudioRestriction(
        string sourceFormat, string targetFormat, string tierName)
    {
        var sourceFileFormat = SupportedConversions.ParseFormat(sourceFormat);
        var targetFileFormat = SupportedConversions.ParseFormat(targetFormat);

        if (sourceFileFormat.HasValue)
        {
            var sourceCategory = SupportedConversions.GetCategory(sourceFileFormat.Value);
            if (sourceCategory is FormatCategory.Video or FormatCategory.Audio)
            {
                return (false, $"Video and audio conversions are not available on the {tierName} tier. Please upgrade to Pro or Business to convert media files.");
            }
        }

        if (targetFileFormat.HasValue)
        {
            var targetCategory = SupportedConversions.GetCategory(targetFileFormat.Value);
            if (targetCategory is FormatCategory.Video or FormatCategory.Audio)
            {
                return (false, $"Video and audio conversions are not available on the {tierName} tier. Please upgrade to Pro or Business to convert media files.");
            }
        }

        return (true, null);
    }

    private static (int DailyLimit, long MaxFileSize) GetTierLimits(UserTier tier) => tier switch
    {
        UserTier.Free => (FreeDailyLimit, FreeMaxFileSize),
        UserTier.Pro => (ProDailyLimit, ProMaxFileSize),
        UserTier.Business => (BusinessDailyLimit, BusinessMaxFileSize),
        _ => (FreeDailyLimit, FreeMaxFileSize)
    };

    private static int GetDefaultDailyLimit(UserTier tier) => tier switch
    {
        UserTier.Free => FreeDailyLimit,
        UserTier.Pro => ProDailyLimit,
        UserTier.Business => BusinessDailyLimit,
        _ => FreeDailyLimit
    };

    private static long GetDefaultMaxFileSize(UserTier tier) => tier switch
    {
        UserTier.Free => FreeMaxFileSize,
        UserTier.Pro => ProMaxFileSize,
        UserTier.Business => BusinessMaxFileSize,
        _ => FreeMaxFileSize
    };

    private static string FormatFileSize(long bytes)
    {
        if (bytes >= 1L * 1024 * 1024 * 1024)
            return $"{bytes / (1024 * 1024 * 1024)}GB";
        return $"{bytes / (1024 * 1024)}MB";
    }
}
