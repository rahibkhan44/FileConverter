using System.Text;
using FileConverter.Shared;

namespace FileConverter.Web.Endpoints;

public static class SitemapEndpoint
{
    public static void MapSitemapEndpoint(this WebApplication app)
    {
        app.MapGet("/sitemap.xml", (HttpContext ctx, IConfiguration config) =>
        {
            var baseUrl = (config["SiteBaseUrl"] ?? "https://fileconverter.com").TrimEnd('/');
            var sb = new StringBuilder();

            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

            // Home page — highest priority
            AppendUrl(sb, baseUrl, "/", "weekly", "1.0");

            // Tool pages — high priority
            foreach (var toolPath in new[] { "/tools/pdf", "/tools/images", "/tools/video", "/tools/audio" })
            {
                AppendUrl(sb, baseUrl, toolPath, "weekly", "0.9");
            }

            // Static pages
            AppendUrl(sb, baseUrl, "/formats", "weekly", "0.8");
            AppendUrl(sb, baseUrl, "/pricing", "weekly", "0.8");
            AppendUrl(sb, baseUrl, "/about", "weekly", "0.8");

            // Per-extension format pages
            var allConversions = ClientSupportedFormats.GetAll();
            var allExtensions = ClientSupportedFormats.AllSupportedExtensions;

            foreach (var ext in allExtensions.OrderBy(e => e, StringComparer.OrdinalIgnoreCase))
            {
                AppendUrl(sb, baseUrl, $"/formats/{ext}", "weekly", "0.8");
            }

            // Conversion pair pages
            foreach (var (source, targets) in allConversions.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                foreach (var target in targets.OrderBy(t => t, StringComparer.OrdinalIgnoreCase))
                {
                    if (source.Equals(target, StringComparison.OrdinalIgnoreCase))
                        continue;

                    AppendUrl(sb, baseUrl, $"/convert/{source}-to-{target}", "weekly", "0.7");
                }
            }

            sb.AppendLine("</urlset>");

            return Results.Content(sb.ToString(), "application/xml", Encoding.UTF8);
        });
    }

    private static void AppendUrl(StringBuilder sb, string baseUrl, string path, string changefreq, string priority)
    {
        sb.AppendLine("  <url>");
        sb.AppendLine($"    <loc>{baseUrl}{path}</loc>");
        sb.AppendLine($"    <changefreq>{changefreq}</changefreq>");
        sb.AppendLine($"    <priority>{priority}</priority>");
        sb.AppendLine("  </url>");
    }
}
