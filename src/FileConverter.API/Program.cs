using System.Text;
using System.Threading.RateLimiting;
using FileConverter.API.BackgroundServices;
using FileConverter.API.Middleware;
using FileConverter.Application.Services;
using FileConverter.Domain.Interfaces;
using FileConverter.Infrastructure.Converters;
using FileConverter.Infrastructure.Data;
using FileConverter.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using FileConverter.API.Hubs;
using QuestPDF.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

// QuestPDF license
QuestPDF.Settings.License = LicenseType.Community;

// Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "FileConverter API", Version = "v1" });
});

// CORS for Blazor/MAUI clients
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .WithExposedHeaders("X-Correlation-Id");
    });
});

// EF Core — SQLite for dev, configure PostgreSQL via connection string for prod
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=fileconverter.db";
    options.UseSqlite(connectionString);
});

// ASP.NET Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "FileConverterDefaultSecretKey2024!@#$%^&*()MinLength32";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "FileConverter",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "FileConverter",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddAuthorization();

// Domain services
builder.Services.AddSingleton<IFileStorageService>(new TempFileStorageService());
builder.Services.AddScoped<IJobTracker, EfJobTracker>();
builder.Services.AddSingleton<IRateLimitService, RateLimitService>();
builder.Services.AddSingleton<IJobQueue, ChannelJobQueue>();

// Converters — Magick.NET first (wins all image routes), then specialized converters
builder.Services.AddSingleton<IFileConverter, MagickImageConverter>();
builder.Services.AddSingleton<IFileConverter, NpoiDocumentConverter>();
builder.Services.AddSingleton<IFileConverter, NpoiSpreadsheetConverter>();
builder.Services.AddSingleton<IFileConverter, NpoiPresentationConverter>();
builder.Services.AddSingleton<IFileConverter, MarkdownConverter>();
builder.Services.AddSingleton<IFileConverter, SvgConverter>();
builder.Services.AddSingleton<IFileConverter, PdfConverter>();
builder.Services.AddSingleton<IFileConverter, ImageToPdfConverter>();
builder.Services.AddSingleton<IFileConverter, PdfToImageConverter>();
builder.Services.AddSingleton<IFileConverter, VideoConverter>();
builder.Services.AddSingleton<IFileConverter, AudioConverter>();
builder.Services.AddSingleton<ConversionEngineFactory>();

// Tool services (PDF tools, Image tools)
builder.Services.AddSingleton<PdfToolsService>();
builder.Services.AddSingleton<ImageToolsService>();

// Webhook support
builder.Services.AddHttpClient("Webhooks");
builder.Services.AddSingleton<IWebhookService, WebhookService>();

// Tier enforcement
builder.Services.AddScoped<ITierEnforcementService, TierEnforcementService>();

// Application services
builder.Services.AddScoped<ConversionService>();

// SignalR
builder.Services.AddSignalR();

// Background workers
builder.Services.AddHostedService<ConversionWorker>();
builder.Services.AddHostedService<CleanupWorker>();

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

// Rate limiting
var rateConfig = builder.Configuration.GetSection("FileConverter:RateLimit");
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("api", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateConfig.GetValue("RequestsPerMinute", 30),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// Response compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/json"]);
});

var app = builder.Build();

// Auto-create/migrate database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Middleware pipeline (order matters)
app.UseResponseCompression();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseCors("AllowAll");
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "FileConverter API v1"));
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseMiddleware<ApiKeyAuthMiddleware>();
app.UseAuthorization();

// Health check endpoints
app.MapHealthChecks("/health/live", new()
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready");

app.MapControllers().RequireRateLimiting("api");
app.MapHub<ConversionProgressHub>("/hubs/progress");

Log.Information("FileConverter API starting...");
app.Run();

public partial class Program { } // For integration tests
