using FileConverter.Shared;
using FileConverter.Web.Components;
using FileConverter.Web.Endpoints;

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
app.MapSitemapEndpoint();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
