using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FileConverter.Application;
using FileConverter.Domain.Enums;
using FileConverter.Domain.Interfaces;
using HtmlAgilityPack;
using RtfPipe;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FileConverter.Infrastructure.Converters;

/// <summary>
/// Handles all document-to-document conversions using NPOI + OpenXml + RtfPipe + HtmlAgilityPack.
/// Replaces LibreOfficeConverter for document routes.
/// Supports: DOCX, ODT, RTF, TXT, HTML → any of the above + PDF
/// </summary>
public class NpoiDocumentConverter : IFileConverter
{
    private static readonly HashSet<FileFormat> DocumentFormats = new()
    {
        FileFormat.Docx, FileFormat.Odt, FileFormat.Rtf, FileFormat.Txt, FileFormat.Html
    };

    private static readonly HashSet<FileFormat> ValidTargets = new()
    {
        FileFormat.Docx, FileFormat.Odt, FileFormat.Rtf, FileFormat.Txt, FileFormat.Html, FileFormat.Pdf
    };

    public bool CanConvert(FileFormat source, FileFormat target)
        => DocumentFormats.Contains(source) && ValidTargets.Contains(target) && source != target;

    public async Task<string> ConvertAsync(string inputPath, string outputDirectory, FileFormat targetFormat,
        Dictionary<string, string> options, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(5);

        var sourceFormat = SupportedConversions.ParseFormat(Path.GetExtension(inputPath))
            ?? throw new InvalidOperationException("Cannot determine source format.");

        var outputFileName = Path.GetFileNameWithoutExtension(inputPath) + SupportedConversions.GetExtension(targetFormat);
        var outputPath = Path.Combine(outputDirectory, outputFileName);
        Directory.CreateDirectory(outputDirectory);

        progress?.Report(10);

        // Step 1: Extract text/content from source
        var content = await ExtractContentAsync(inputPath, sourceFormat, cancellationToken);
        progress?.Report(50);

        // Step 2: Write to target format
        await WriteToTargetAsync(content, outputPath, targetFormat, options, cancellationToken);
        progress?.Report(100);

        return outputPath;
    }

    private static async Task<DocumentContent> ExtractContentAsync(string inputPath, FileFormat format, CancellationToken ct)
    {
        return format switch
        {
            FileFormat.Docx => ExtractFromDocx(inputPath),
            FileFormat.Odt => ExtractFromOdt(inputPath),
            FileFormat.Rtf => await ExtractFromRtfAsync(inputPath, ct),
            FileFormat.Txt => new DocumentContent { PlainText = await File.ReadAllTextAsync(inputPath, ct) },
            FileFormat.Html => await ExtractFromHtmlAsync(inputPath, ct),
            _ => throw new NotSupportedException($"Cannot read {format}")
        };
    }

    private static DocumentContent ExtractFromDocx(string inputPath)
    {
        using var doc = WordprocessingDocument.Open(inputPath, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null) return new DocumentContent { PlainText = "" };

        var paragraphs = new List<string>();
        foreach (var para in body.Elements<Paragraph>())
        {
            paragraphs.Add(para.InnerText);
        }

        // Also try to extract table content
        foreach (var table in body.Elements<Table>())
        {
            foreach (var row in table.Elements<TableRow>())
            {
                var cells = row.Elements<TableCell>().Select(c => c.InnerText);
                paragraphs.Add(string.Join("\t", cells));
            }
        }

        return new DocumentContent { PlainText = string.Join(Environment.NewLine, paragraphs) };
    }

    private static DocumentContent ExtractFromOdt(string inputPath)
    {
        // ODT is a ZIP archive with content.xml inside
        using var zip = ZipFile.OpenRead(inputPath);
        var contentEntry = zip.GetEntry("content.xml");
        if (contentEntry == null) return new DocumentContent { PlainText = "" };

        using var stream = contentEntry.Open();
        var xdoc = XDocument.Load(stream);

        // Extract all text:p elements from the OpenDocument namespace
        XNamespace textNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        var paragraphs = xdoc.Descendants(textNs + "p")
            .Select(p => p.Value)
            .ToList();

        return new DocumentContent { PlainText = string.Join(Environment.NewLine, paragraphs) };
    }

    private static async Task<DocumentContent> ExtractFromRtfAsync(string inputPath, CancellationToken ct)
    {
        var rtfContent = await File.ReadAllTextAsync(inputPath, ct);
        using var reader = new StringReader(rtfContent);
        var html = Rtf.ToHtml(reader);
        var plainText = StripHtml(html);
        return new DocumentContent { PlainText = plainText, Html = html };
    }

    private static async Task<DocumentContent> ExtractFromHtmlAsync(string inputPath, CancellationToken ct)
    {
        var htmlContent = await File.ReadAllTextAsync(inputPath, ct);
        var doc = new HtmlDocument();
        doc.LoadHtml(htmlContent);
        var plainText = doc.DocumentNode.InnerText;
        plainText = System.Net.WebUtility.HtmlDecode(plainText);
        return new DocumentContent { PlainText = plainText, Html = htmlContent };
    }

    private static async Task WriteToTargetAsync(DocumentContent content, string outputPath, FileFormat target,
        Dictionary<string, string> options, CancellationToken ct)
    {
        switch (target)
        {
            case FileFormat.Txt:
                await File.WriteAllTextAsync(outputPath, content.PlainText, ct);
                break;

            case FileFormat.Pdf:
                PdfGenerationHelper.GeneratePdfFromText(content.PlainText, outputPath, options);
                break;

            case FileFormat.Docx:
                WriteDocx(content.PlainText, outputPath);
                break;

            case FileFormat.Odt:
                WriteOdt(content.PlainText, outputPath);
                break;

            case FileFormat.Rtf:
                WriteRtf(content.PlainText, outputPath);
                break;

            case FileFormat.Html:
                await WriteHtmlAsync(content, outputPath, ct);
                break;

            default:
                throw new NotSupportedException($"Cannot write to {target}");
        }
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

    private static void WriteOdt(string text, string outputPath)
    {
        // Create a minimal ODT (OpenDocument Text) file
        // ODT is a ZIP archive with specific XML files
        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

        // mimetype (must be first entry, uncompressed)
        var mimeEntry = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
        using (var writer = new StreamWriter(mimeEntry.Open()))
        {
            writer.Write("application/vnd.oasis.opendocument.text");
        }

        // content.xml
        XNamespace officeNs = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
        XNamespace textNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

        var contentXml = new XDocument(
            new XElement(officeNs + "document-content",
                new XAttribute(XNamespace.Xmlns + "office", officeNs),
                new XAttribute(XNamespace.Xmlns + "text", textNs),
                new XAttribute(officeNs + "version", "1.2"),
                new XElement(officeNs + "body",
                    new XElement(officeNs + "text",
                        text.Split('\n').Select(line =>
                            new XElement(textNs + "p", line)
                        )
                    )
                )
            )
        );

        var contentEntry = archive.CreateEntry("content.xml");
        using (var stream = contentEntry.Open())
        {
            contentXml.Save(stream);
        }

        // META-INF/manifest.xml
        XNamespace manifestNs = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";
        var manifestXml = new XDocument(
            new XElement(manifestNs + "manifest",
                new XAttribute(XNamespace.Xmlns + "manifest", manifestNs),
                new XElement(manifestNs + "file-entry",
                    new XAttribute(manifestNs + "media-type", "application/vnd.oasis.opendocument.text"),
                    new XAttribute(manifestNs + "full-path", "/")),
                new XElement(manifestNs + "file-entry",
                    new XAttribute(manifestNs + "media-type", "text/xml"),
                    new XAttribute(manifestNs + "full-path", "content.xml"))
            )
        );

        var manifestEntry = archive.CreateEntry("META-INF/manifest.xml");
        using (var stream = manifestEntry.Open())
        {
            manifestXml.Save(stream);
        }
    }

    private static void WriteRtf(string text, string outputPath)
    {
        // Create a minimal RTF document
        var sb = new StringBuilder();
        sb.AppendLine(@"{\rtf1\ansi\deff0");
        sb.AppendLine(@"{\fonttbl{\f0 Calibri;}}");
        sb.AppendLine(@"\pard");

        foreach (var line in text.Split('\n'))
        {
            // Escape RTF special characters
            var escaped = line
                .Replace("\\", "\\\\")
                .Replace("{", "\\{")
                .Replace("}", "\\}");
            sb.AppendLine(escaped + @"\par");
        }

        sb.AppendLine("}");
        File.WriteAllText(outputPath, sb.ToString(), Encoding.ASCII);
    }

    private static async Task WriteHtmlAsync(DocumentContent content, string outputPath, CancellationToken ct)
    {
        // If we already have HTML from RTF conversion, use it
        if (!string.IsNullOrEmpty(content.Html))
        {
            await File.WriteAllTextAsync(outputPath, content.Html, ct);
            return;
        }

        // Otherwise generate basic HTML from plain text
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><meta charset=\"utf-8\"><title>Converted Document</title></head><body>");

        foreach (var line in content.PlainText.Split('\n'))
        {
            sb.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(line)}</p>");
        }

        sb.AppendLine("</body></html>");
        await File.WriteAllTextAsync(outputPath, sb.ToString(), ct);
    }

    private static string StripHtml(string html)
    {
        var text = Regex.Replace(html, "<br\\s*/?>", "\n");
        text = Regex.Replace(text, "</p>", "\n");
        text = Regex.Replace(text, "<[^>]+>", "");
        text = System.Net.WebUtility.HtmlDecode(text);
        return text.Trim();
    }

    private class DocumentContent
    {
        public string PlainText { get; set; } = "";
        public string? Html { get; set; }
    }
}
