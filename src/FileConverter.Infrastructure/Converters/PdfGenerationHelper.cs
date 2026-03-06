using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FileConverter.Infrastructure.Converters;

/// <summary>
/// Shared helper for generating PDFs from text content using QuestPDF.
/// Used by multiple converters to avoid code duplication.
/// </summary>
public static class PdfGenerationHelper
{
    public static void GeneratePdfFromText(string text, string outputPath, Dictionary<string, string>? options = null)
    {
        options ??= new();

        var pageSize = GetPageSize(options);
        bool landscape = options.TryGetValue("orientation", out var o) && o.Equals("Landscape", StringComparison.OrdinalIgnoreCase);
        if (landscape) pageSize = new PageSize(pageSize.Height, pageSize.Width);

        float margin = options.TryGetValue("marginMm", out var m) && float.TryParse(m, out var mv) ? mv : 10;
        float fontSize = options.TryGetValue("fontSize", out var fs) && float.TryParse(fs, out var fsv) ? fsv : 12;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(pageSize);
                page.Margin(margin, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontSize(fontSize));
                page.Content().Column(col =>
                {
                    col.Item().Text(text);
                });
            });
        }).GeneratePdf(outputPath);
    }

    public static void GeneratePdfFromImage(byte[] imageBytes, string outputPath, Dictionary<string, string>? options = null)
    {
        options ??= new();

        var pageSize = GetPageSize(options);
        bool landscape = options.TryGetValue("orientation", out var o) && o.Equals("Landscape", StringComparison.OrdinalIgnoreCase);
        if (landscape) pageSize = new PageSize(pageSize.Height, pageSize.Width);

        float margin = options.TryGetValue("marginMm", out var m) && float.TryParse(m, out var mv) ? mv : 10;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(pageSize);
                page.Margin(margin, Unit.Millimetre);
                page.Content().Image(imageBytes).FitArea();
            });
        }).GeneratePdf(outputPath);
    }

    public static PageSize GetPageSize(Dictionary<string, string> options)
    {
        var size = options.TryGetValue("pageSize", out var ps) ? ps : "A4";
        return size.ToUpperInvariant() switch
        {
            "A3" => PageSizes.A3,
            "A5" => PageSizes.A5,
            "LETTER" => PageSizes.Letter,
            "LEGAL" => PageSizes.Legal,
            _ => PageSizes.A4
        };
    }
}
