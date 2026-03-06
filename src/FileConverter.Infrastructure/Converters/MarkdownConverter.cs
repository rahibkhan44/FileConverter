using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FileConverter.Application;
using FileConverter.Domain.Enums;
using FileConverter.Domain.Interfaces;
using Markdig;
using System.Text;
using System.Text.RegularExpressions;

namespace FileConverter.Infrastructure.Converters;

/// <summary>
/// Converts Markdown files to HTML, PDF, TXT, and DOCX using Markdig.
/// </summary>
public class MarkdownConverter : IFileConverter
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public bool CanConvert(FileFormat source, FileFormat target)
        => source == FileFormat.Md && target is FileFormat.Html or FileFormat.Pdf or FileFormat.Txt or FileFormat.Docx;

    public async Task<string> ConvertAsync(string inputPath, string outputDirectory, FileFormat targetFormat,
        Dictionary<string, string> options, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(10);

        var markdown = await File.ReadAllTextAsync(inputPath, cancellationToken);
        var outputFileName = Path.GetFileNameWithoutExtension(inputPath) + SupportedConversions.GetExtension(targetFormat);
        var outputPath = Path.Combine(outputDirectory, outputFileName);
        Directory.CreateDirectory(outputDirectory);

        progress?.Report(30);

        switch (targetFormat)
        {
            case FileFormat.Html:
                var html = RenderToHtml(markdown);
                await File.WriteAllTextAsync(outputPath, html, cancellationToken);
                break;

            case FileFormat.Pdf:
                var pdfHtml = Markdig.Markdown.ToHtml(markdown, Pipeline);
                var plainText = StripHtml(pdfHtml);
                PdfGenerationHelper.GeneratePdfFromText(plainText, outputPath, options);
                break;

            case FileFormat.Txt:
                var txtHtml = Markdig.Markdown.ToHtml(markdown, Pipeline);
                var text = StripHtml(txtHtml);
                await File.WriteAllTextAsync(outputPath, text, cancellationToken);
                break;

            case FileFormat.Docx:
                var docHtml = Markdig.Markdown.ToHtml(markdown, Pipeline);
                var docText = StripHtml(docHtml);
                WriteDocx(docText, outputPath);
                break;
        }

        progress?.Report(100);
        return outputPath;
    }

    private static string RenderToHtml(string markdown)
    {
        var body = Markdig.Markdown.ToHtml(markdown, Pipeline);
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><meta charset=\"utf-8\"><title>Markdown</title>");
        sb.AppendLine("<style>body{font-family:sans-serif;max-width:900px;margin:0 auto;padding:20px;line-height:1.6}code{background:#f4f4f4;padding:2px 6px;border-radius:3px}pre code{display:block;padding:12px;overflow-x:auto}blockquote{border-left:4px solid #ddd;margin:0;padding-left:16px;color:#666}table{border-collapse:collapse;width:100%}td,th{border:1px solid #ddd;padding:8px}</style>");
        sb.AppendLine("</head><body>");
        sb.Append(body);
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static string StripHtml(string html)
    {
        var text = Regex.Replace(html, "<br\\s*/?>", "\n");
        text = Regex.Replace(text, "</p>|</div>|</li>|</h[1-6]>", "\n");
        text = Regex.Replace(text, "<[^>]+>", "");
        text = System.Net.WebUtility.HtmlDecode(text);
        return text.Trim();
    }

    private static void WriteDocx(string text, string outputPath)
    {
        using var doc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
        var body = mainPart.Document.AppendChild(new Body());

        foreach (var line in text.Split('\n'))
        {
            var para = body.AppendChild(new Paragraph());
            var run = para.AppendChild(new Run());
            run.AppendChild(new Text(line) { Space = SpaceProcessingModeValues.Preserve });
        }
    }
}
