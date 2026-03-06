using FileConverter.API.BackgroundServices;
using FileConverter.Application.Services;
using FileConverter.Domain.Interfaces;
using FileConverter.Infrastructure.Converters;
using FileConverter.Infrastructure.Services;
using QuestPDF.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
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
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// Domain services (singletons for in-memory state)
builder.Services.AddSingleton<IFileStorageService>(new TempFileStorageService());
builder.Services.AddSingleton<IJobTracker, InMemoryJobTracker>();
builder.Services.AddSingleton<IRateLimitService, RateLimitService>();

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

// Application services
builder.Services.AddScoped<ConversionService>();

// Background workers
builder.Services.AddHostedService<ConversionWorker>();
builder.Services.AddHostedService<CleanupWorker>();

var app = builder.Build();

// Middleware
app.UseCors("AllowAll");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "FileConverter API v1"));
}

app.UseHttpsRedirection();
app.MapControllers();

Log.Information("FileConverter API starting...");
app.Run();

public partial class Program { } // For integration tests
