using FileConverter.Application;
using FileConverter.Domain.Enums;
using FileConverter.Domain.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RtfPipe;

namespace FileConverter.Infrastructure.Converters;

public class RtfConverter : IFileConverter
{
    public bool CanConvert(FileFormat source, FileFormat target)
    {
        return source == FileFormat.Rtf && target is FileFormat.Txt or FileFormat.Pdf or FileFormat.Docx;
    }

    public async Task<string> ConvertAsync(string inputPath, string outputDirectory, FileFormat targetFormat,
        Dictionary<string, string> options, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(10);

        var outputFileName = Path.GetFileNameWithoutExtension(inputPath) + SupportedConversions.GetExtension(targetFormat);
        var outputPath = Path.Combine(outputDirectory, outputFileName);

        var rtfContent = await File.ReadAllTextAsync(inputPath, cancellationToken);
        progress?.Report(30);

        // Extract plain text from RTF
        string plainText;
        using (var reader = new StringReader(rtfContent))
        {
            var html = Rtf.ToHtml(reader);
            // Strip HTML tags to get plain text
            plainText = StripHtml(html);
        }

        progress?.Report(60);

        switch (targetFormat)
        {
            case FileFormat.Txt:
                await File.WriteAllTextAsync(outputPath, plainText, cancellationToken);
                break;

            case FileFormat.Pdf:
                GeneratePdfFromText(plainText, outputPath, options);
                break;

            case FileFormat.Docx:
                GenerateDocxFromText(plainText, outputPath);
                break;
        }

        progress?.Report(100);
        return outputPath;
    }

    private static string StripHtml(string html)
    {
        // Simple HTML tag removal
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<br\\s*/?>", "\n");
        text = System.Text.RegularExpressions.Regex.Replace(text, "</p>", "\n");
        text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");
        text = System.Net.WebUtility.HtmlDecode(text);
        return text.Trim();
    }

    private static void GeneratePdfFromText(string text, string outputPath, Dictionary<string, string> options)
    {
        float margin = options.TryGetValue("marginMm", out var m) && float.TryParse(m, out var mv) ? mv : 10;
        float fontSize = options.TryGetValue("fontSize", out var fs) && float.TryParse(fs, out var fsv) ? fsv : 12;

        QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(margin, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontSize(fontSize));
                page.Content().Text(text);
            });
        }).GeneratePdf(outputPath);
    }

    private static void GenerateDocxFromText(string text, string outputPath)
    {
        using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(outputPath,
            DocumentFormat.OpenXml.WordprocessingDocumentType.Document);

        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
        var body = mainPart.Document.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Body());

        foreach (var line in text.Split('\n'))
        {
            var para = body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph());
            var run = para.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Run());
            run.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Text(line));
        }
    }
}
