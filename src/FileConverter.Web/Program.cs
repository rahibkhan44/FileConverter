using System.Text;
using FileConverter.Shared;
using FileConverter.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// API client
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:5001";
builder.Services.AddHttpClient<FileConverterApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromMinutes(5);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

// Dynamic sitemap.xml
app.MapGet("/sitemap.xml", (HttpContext ctx) =>
{
    var baseUrl = "https://fileconverter.com";
    var sb = new StringBuilder();
    sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
    sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

    foreach (var path in new[] { "/", "/formats", "/pricing", "/about",
        "/tools/pdf", "/tools/images", "/tools/video", "/tools/audio" })
    {
        sb.AppendLine($"  <url><loc>{baseUrl}{path}</loc><changefreq>weekly</changefreq><priority>0.8</priority></url>");
    }

    var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "jpeg", "tif", "htm", "markdown", "heif", "mts", "oga" };

    foreach (var (source, targets) in ClientSupportedFormats.GetAll())
    {
        if (aliases.Contains(source)) continue;
        foreach (var target in targets)
        {
            if (source.Equals(target, StringComparison.OrdinalIgnoreCase)) continue;
            sb.AppendLine($"  <url><loc>{baseUrl}/convert/{source}-to-{target}</loc><changefreq>monthly</changefreq><priority>0.6</priority></url>");
        }
    }

    sb.AppendLine("</urlset>");
    return Results.Content(sb.ToString(), "application/xml");
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
