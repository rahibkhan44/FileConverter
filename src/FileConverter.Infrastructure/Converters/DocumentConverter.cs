using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FileConverter.Application;
using FileConverter.Domain.Enums;
using FileConverter.Domain.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;

namespace FileConverter.Infrastructure.Converters;

public class DocumentConverter : IFileConverter
{
    public bool CanConvert(FileFormat source, FileFormat target)
    {
        return (source, target) switch
        {
            (FileFormat.Docx, FileFormat.Txt) => true,
            (FileFormat.Docx, FileFormat.Pdf) => true,
            (FileFormat.Pptx, FileFormat.Txt) => true,
            (FileFormat.Pptx, FileFormat.Pdf) => true,
            _ => false
        };
    }

    public async Task<string> ConvertAsync(string inputPath, string outputDirectory, FileFormat targetFormat,
        Dictionary<string, string> options, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(10);

        var sourceFormat = SupportedConversions.ParseFormat(Path.GetExtension(inputPath))
            ?? throw new InvalidOperationException("Cannot determine source format.");

        var outputFileName = Path.GetFileNameWithoutExtension(inputPath) + SupportedConversions.GetExtension(targetFormat);
        var outputPath = Path.Combine(outputDirectory, outputFileName);

        if (sourceFormat == FileFormat.Docx)
        {
            await ConvertDocxAsync(inputPath, outputPath, targetFormat, options, progress, cancellationToken);
        }
        else if (sourceFormat == FileFormat.Pptx)
        {
            await ConvertPptxAsync(inputPath, outputPath, targetFormat, options, progress, cancellationToken);
        }

        return outputPath;
    }

    private async Task ConvertDocxAsync(string inputPath, string outputPath, FileFormat targetFormat,
        Dictionary<string, string> options, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(20);

        using var doc = WordprocessingDocument.Open(inputPath, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null) throw new InvalidOperationException("Cannot read DOCX document body.");

        var sb = new StringBuilder();
        foreach (var paragraph in body.Elements<Paragraph>())
        {
            sb.AppendLine(paragraph.InnerText);
        }

        var text = sb.ToString();
        progress?.Report(50);

        if (targetFormat == FileFormat.Txt)
        {
            await File.WriteAllTextAsync(outputPath, text, cancellationToken);
        }
        else if (targetFormat == FileFormat.Pdf)
        {
            GeneratePdfFromText(text, outputPath, options);
        }

        progress?.Report(100);
    }

    private async Task ConvertPptxAsync(string inputPath, string outputPath, FileFormat targetFormat,
        Dictionary<string, string> options, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(20);

        using var ppt = PresentationDocument.Open(inputPath, false);
        var slides = ppt.PresentationPart?.SlideParts;

        var sb = new StringBuilder();
        int slideNum = 1;
        if (slides != null)
        {
            foreach (var slide in slides)
            {
                cancellationToken.ThrowIfCancellationRequested();
                sb.AppendLine($"--- Slide {slideNum++} ---");
                var texts = slide.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>();
                foreach (var t in texts)
                {
                    if (!string.IsNullOrWhiteSpace(t.Text))
                        sb.AppendLine(t.Text);
                }
                sb.AppendLine();
            }
        }

        var text = sb.ToString();
        progress?.Report(50);

        if (targetFormat == FileFormat.Txt)
        {
            await File.WriteAllTextAsync(outputPath, text, cancellationToken);
        }
        else if (targetFormat == FileFormat.Pdf)
        {
            GeneratePdfFromText(text, outputPath, options);
        }

        progress?.Report(100);
    }

    private static void GeneratePdfFromText(string text, string outputPath, Dictionary<string, string> options)
    {
        var pageSizeStr = options.TryGetValue("pageSize", out var ps) ? ps : "A4";
        QuestPDF.Helpers.PageSize pageSize = pageSizeStr.ToUpperInvariant() switch
        {
            "A3" => PageSizes.A3,
            "A5" => PageSizes.A5,
            "LETTER" => PageSizes.Letter,
            "LEGAL" => PageSizes.Legal,
            _ => PageSizes.A4
        };

        bool landscape = options.TryGetValue("orientation", out var o) && o.Equals("Landscape", StringComparison.OrdinalIgnoreCase);
        if (landscape) pageSize = new QuestPDF.Helpers.PageSize(pageSize.Height, pageSize.Width);

        float margin = options.TryGetValue("marginMm", out var m) && float.TryParse(m, out var mv) ? mv : 10;
        float fontSize = options.TryGetValue("fontSize", out var fs) && float.TryParse(fs, out var fsv) ? fsv : 12;

        QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(pageSize);
                page.Margin(margin, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontSize(fontSize));
                page.Content().Text(text);
            });
        }).GeneratePdf(outputPath);
    }
}
