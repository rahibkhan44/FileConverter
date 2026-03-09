using FileConverter.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace FileConverter.Infrastructure.Data;

public class ApplicationUser : IdentityUser
{
    public UserTier Tier { get; set; } = UserTier.Free;
    public int DailyConversionLimit { get; set; } = 20;
    public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024; // 100MB free tier
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public int TotalConversions { get; set; }
}
