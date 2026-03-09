using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FileConverter.Domain.Models;
using FileConverter.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace FileConverter.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;

    public AuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager,
        IConfiguration config, AppDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _config = config;
        _db = db;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        var token = GenerateJwtToken(user);
        return Ok(new AuthResponse { Token = token, Email = user.Email!, ExpiresAt = DateTime.UtcNow.AddDays(7) });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return Unauthorized(new { error = "Invalid email or password." });

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Unauthorized(new { error = "Invalid email or password." });

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var token = GenerateJwtToken(user);
        return Ok(new AuthResponse { Token = token, Email = user.Email!, ExpiresAt = DateTime.UtcNow.AddDays(7) });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetProfile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        return Ok(new UserProfile
        {
            Email = user.Email!,
            Tier = user.Tier.ToString(),
            DailyConversionLimit = user.DailyConversionLimit,
            MaxFileSizeMB = user.MaxFileSizeBytes / (1024 * 1024),
            TotalConversions = user.TotalConversions,
            CreatedAt = user.CreatedAt
        });
    }

    [Authorize]
    [HttpPost("api-keys")]
    public async Task<IActionResult> CreateApiKey([FromBody] CreateApiKeyRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var keyCount = await _db.ApiKeys.CountAsync(k => k.UserId == user.Id && !k.IsRevoked);
        if (keyCount >= 5)
            return BadRequest(new { error = "Maximum 5 active API keys allowed." });

        var rawKey = $"fc_{Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLower()}";
        var keyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))).ToLower();

        var apiKey = new ApiKey
        {
            UserId = user.Id,
            KeyHash = keyHash,
            KeyPrefix = rawKey[..10],
            Name = request.Name ?? "Default",
            ExpiresAt = request.ExpiresInDays > 0 ? DateTime.UtcNow.AddDays(request.ExpiresInDays) : null
        };

        _db.ApiKeys.Add(apiKey);
        await _db.SaveChangesAsync();

        return Ok(new { apiKey.Id, Key = rawKey, apiKey.Name, apiKey.CreatedAt, apiKey.ExpiresAt });
    }

    [Authorize]
    [HttpGet("api-keys")]
    public async Task<IActionResult> ListApiKeys()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var keys = await _db.ApiKeys
            .Where(k => k.UserId == user.Id && !k.IsRevoked)
            .Select(k => new { k.Id, k.KeyPrefix, k.Name, k.CreatedAt, k.LastUsedAt, k.ExpiresAt })
            .ToListAsync();

        return Ok(keys);
    }

    [Authorize]
    [HttpDelete("api-keys/{id:guid}")]
    public async Task<IActionResult> RevokeApiKey(Guid id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var key = await _db.ApiKeys.FirstOrDefaultAsync(k => k.Id == id && k.UserId == user.Id);
        if (key == null) return NotFound();

        key.IsRevoked = true;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize]
    [HttpGet("history")]
    public async Task<IActionResult> GetConversionHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        // For now, return recent jobs (all jobs — in future, filter by UserId on ConversionJob)
        var jobs = await _db.Jobs
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new
            {
                j.Id, j.OriginalFileName, j.SourceFormat, j.TargetFormat,
                Status = j.Status.ToString(), j.CreatedAt, j.CompletedAt
            })
            .ToListAsync();

        return Ok(jobs);
    }

    private string GenerateJwtToken(ApplicationUser user)
    {
        var jwtKey = _config["Jwt:Key"] ?? "FileConverterDefaultSecretKey2024!@#$%^&*()MinLength32";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email!),
            new Claim("tier", user.Tier.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "FileConverter",
            audience: _config["Jwt:Audience"] ?? "FileConverter",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class UserProfile
{
    public string Email { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public int DailyConversionLimit { get; set; }
    public long MaxFileSizeMB { get; set; }
    public int TotalConversions { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateApiKeyRequest
{
    public string? Name { get; set; }
    public int ExpiresInDays { get; set; }
}
