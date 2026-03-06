using DocumentFormat.OpenXml.Packaging;
using FileConverter.Application;
using FileConverter.Domain.Enums;
using FileConverter.Domain.Interfaces;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace FileConverter.Infrastructure.Converters;

/// <summary>
/// Handles all presentation conversions: PPTX, ODP → any of the above + PDF/TXT/HTML.
/// Uses OpenXml for PPTX, manual ZIP/XML for ODP.
/// Replaces LibreOfficeConverter for presentation routes.
/// </summary>
public class NpoiPresentationConverter : IFileConverter
{
    private static readonly HashSet<FileFormat> PresentationSources = new()
    {
        FileFormat.Pptx, FileFormat.Odp
    };

    private static readonly HashSet<FileFormat> ValidTargets = new()
    {
        FileFormat.Pptx, FileFormat.Odp,
        FileFormat.Pdf, FileFormat.Txt, FileFormat.Html
    };

    public bool CanConvert(FileFormat source, FileFormat target)
        => PresentationSources.Contains(source) && ValidTargets.Contains(target) && source != target;

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

        // Extract slide text from source
        var slides = ExtractSlides(inputPath, sourceFormat);
        progress?.Report(50);

        // Write to target
        await WriteToTargetAsync(slides, outputPath, targetFormat, options, cancellationToken);
        progress?.Report(100);

        return outputPath;
    }

    private static List<SlideContent> ExtractSlides(string inputPath, FileFormat format)
    {
        return format switch
        {
            FileFormat.Pptx => ExtractFromPptx(inputPath),
            FileFormat.Odp => ExtractFromOdp(inputPath),
            _ => throw new NotSupportedException($"Cannot read {format} as presentation")
        };
    }

    private static List<SlideContent> ExtractFromPptx(string inputPath)
    {
        var slides = new List<SlideContent>();
        using var ppt = PresentationDocument.Open(inputPath, false);
        var slideParts = ppt.PresentationPart?.SlideParts;
        if (slideParts == null) return slides;

        int num = 1;
        foreach (var slidePart in slideParts)
        {
            var texts = slidePart.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>()
                .Where(t => !string.IsNullOrWhiteSpace(t.Text))
                .Select(t => t.Text)
                .ToList();

            slides.Add(new SlideContent { Number = num++, TextLines = texts });
        }
        return slides;
    }

    private static List<SlideContent> ExtractFromOdp(string inputPath)
    {
        var slides = new List<SlideContent>();
        using var zip = ZipFile.OpenRead(inputPath);
        var contentEntry = zip.GetEntry("content.xml");
        if (contentEntry == null) return slides;

        using var stream = contentEntry.Open();
        var xdoc = XDocument.Load(stream);

        XNamespace drawNs = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
        XNamespace textNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        XNamespace presentationNs = "urn:oasis:names:tc:opendocument:xmlns:presentation:1.0";

        var pages = xdoc.Descendants(drawNs + "page").ToList();
        int num = 1;
        foreach (var page in pages)
        {
            var texts = page.Descendants(textNs + "p")
                .Select(p => p.Value)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            slides.Add(new SlideContent { Number = num++, TextLines = texts });
        }
        return slides;
    }

    private static async Task WriteToTargetAsync(List<SlideContent> slides, string outputPath,
        FileFormat target, Dictionary<string, string> options, CancellationToken ct)
    {
        switch (target)
        {
            case FileFormat.Txt:
                await WriteTxtAsync(slides, outputPath, ct);
                break;
            case FileFormat.Pdf:
                WritePdf(slides, outputPath, options);
                break;
            case FileFormat.Html:
                await WriteHtmlAsync(slides, outputPath, ct);
                break;
            case FileFormat.Pptx:
                WritePptx(slides, outputPath);
                break;
            case FileFormat.Odp:
                WriteOdp(slides, outputPath);
                break;
        }
    }

    private static async Task WriteTxtAsync(List<SlideContent> slides, string outputPath, CancellationToken ct)
    {
        var sb = new StringBuilder();
        foreach (var slide in slides)
        {
            sb.AppendLine($"--- Slide {slide.Number} ---");
            foreach (var text in slide.TextLines)
                sb.AppendLine(text);
            sb.AppendLine();
        }
        await File.WriteAllTextAsync(outputPath, sb.ToString(), ct);
    }

    private static void WritePdf(List<SlideContent> slides, string outputPath, Dictionary<string, string> options)
    {
        var sb = new StringBuilder();
        foreach (var slide in slides)
        {
            sb.AppendLine($"--- Slide {slide.Number} ---");
            foreach (var text in slide.TextLines)
                sb.AppendLine(text);
            sb.AppendLine();
        }
        PdfGenerationHelper.GeneratePdfFromText(sb.ToString(), outputPath, options);
    }

    private static async Task WriteHtmlAsync(List<SlideContent> slides, string outputPath, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><meta charset=\"utf-8\"><title>Presentation</title>");
        sb.AppendLine("<style>body{font-family:sans-serif;max-width:900px;margin:0 auto;padding:20px}.slide{border:2px solid #333;border-radius:8px;padding:20px;margin:20px 0;background:#fafafa}.slide-num{color:#666;font-size:0.9em;margin-bottom:10px}</style>");
        sb.AppendLine("</head><body>");

        foreach (var slide in slides)
        {
            sb.AppendLine("<div class=\"slide\">");
            sb.AppendLine($"<div class=\"slide-num\">Slide {slide.Number}</div>");
            foreach (var text in slide.TextLines)
            {
                sb.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(text)}</p>");
            }
            sb.AppendLine("</div>");
        }

        sb.AppendLine("</body></html>");
        await File.WriteAllTextAsync(outputPath, sb.ToString(), ct);
    }

    private static void WritePptx(List<SlideContent> slides, string outputPath)
    {
        using var ppt = PresentationDocument.Create(outputPath, DocumentFormat.OpenXml.PresentationDocumentType.Presentation);

        var presentationPart = ppt.AddPresentationPart();
        presentationPart.Presentation = new DocumentFormat.OpenXml.Presentation.Presentation();

        var slideIdList = presentationPart.Presentation.AppendChild(
            new DocumentFormat.OpenXml.Presentation.SlideIdList());

        uint slideId = 256;
        foreach (var slide in slides)
        {
            var slidePart = presentationPart.AddNewPart<SlidePart>();
            slidePart.Slide = new DocumentFormat.OpenXml.Presentation.Slide(
                new DocumentFormat.OpenXml.Presentation.CommonSlideData(
                    new DocumentFormat.OpenXml.Presentation.ShapeTree(
                        new DocumentFormat.OpenXml.Presentation.NonVisualGroupShapeProperties(
                            new DocumentFormat.OpenXml.Presentation.NonVisualDrawingProperties { Id = 1, Name = "" },
                            new DocumentFormat.OpenXml.Presentation.NonVisualGroupShapeDrawingProperties(),
                            new DocumentFormat.OpenXml.Presentation.ApplicationNonVisualDrawingProperties()),
                        new DocumentFormat.OpenXml.Presentation.GroupShapeProperties()
                    )
                )
            );

            // Add text as a shape
            var shapeTree = slidePart.Slide.CommonSlideData!.ShapeTree!;
            var shape = CreateTextShape(string.Join("\n", slide.TextLines), slideId);
            shapeTree.Append(shape);

            slideIdList.Append(new DocumentFormat.OpenXml.Presentation.SlideId
            {
                Id = slideId++,
                RelationshipId = presentationPart.GetIdOfPart(slidePart)
            });
        }
    }

    private static DocumentFormat.OpenXml.Presentation.Shape CreateTextShape(string text, uint id)
    {
        return new DocumentFormat.OpenXml.Presentation.Shape(
            new DocumentFormat.OpenXml.Presentation.NonVisualShapeProperties(
                new DocumentFormat.OpenXml.Presentation.NonVisualDrawingProperties { Id = id, Name = $"TextBox{id}" },
                new DocumentFormat.OpenXml.Presentation.NonVisualShapeDrawingProperties(),
                new DocumentFormat.OpenXml.Presentation.ApplicationNonVisualDrawingProperties()),
            new DocumentFormat.OpenXml.Presentation.ShapeProperties(
                new DocumentFormat.OpenXml.Drawing.Transform2D(
                    new DocumentFormat.OpenXml.Drawing.Offset { X = 500000, Y = 500000 },
                    new DocumentFormat.OpenXml.Drawing.Extents { Cx = 8000000, Cy = 5000000 })),
            new DocumentFormat.OpenXml.Presentation.TextBody(
                new DocumentFormat.OpenXml.Drawing.BodyProperties(),
                new DocumentFormat.OpenXml.Drawing.Paragraph(
                    new DocumentFormat.OpenXml.Drawing.Run(
                        new DocumentFormat.OpenXml.Drawing.Text(text))))
        );
    }

    private static void WriteOdp(List<SlideContent> slides, string outputPath)
    {
        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

        var mimeEntry = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
        using (var writer = new StreamWriter(mimeEntry.Open()))
        {
            writer.Write("application/vnd.oasis.opendocument.presentation");
        }

        XNamespace officeNs = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
        XNamespace drawNs = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
        XNamespace textNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        XNamespace presentationNs = "urn:oasis:names:tc:opendocument:xmlns:presentation:1.0";

        var slideElements = slides.Select((slide, i) =>
            new XElement(drawNs + "page",
                new XAttribute(drawNs + "name", $"Slide{i + 1}"),
                new XElement(drawNs + "frame",
                    new XAttribute(drawNs + "name", $"TextBox{i + 1}"),
                    slide.TextLines.Select(t =>
                        new XElement(drawNs + "text-box",
                            new XElement(textNs + "p", t))
                    )
                )
            )
        );

        var contentXml = new XDocument(
            new XElement(officeNs + "document-content",
                new XAttribute(XNamespace.Xmlns + "office", officeNs),
                new XAttribute(XNamespace.Xmlns + "draw", drawNs),
                new XAttribute(XNamespace.Xmlns + "text", textNs),
                new XAttribute(XNamespace.Xmlns + "presentation", presentationNs),
                new XAttribute(officeNs + "version", "1.2"),
                new XElement(officeNs + "body",
                    new XElement(officeNs + "presentation", slideElements)
                )
            )
        );

        var contentEntry = archive.CreateEntry("content.xml");
        using (var stream = contentEntry.Open())
        {
            contentXml.Save(stream);
        }

        XNamespace manifestNs = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";
        var manifestXml = new XDocument(
            new XElement(manifestNs + "manifest",
                new XAttribute(XNamespace.Xmlns + "manifest", manifestNs),
                new XElement(manifestNs + "file-entry",
                    new XAttribute(manifestNs + "media-type", "application/vnd.oasis.opendocument.presentation"),
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

    private class SlideContent
    {
        public int Number { get; set; }
        public List<string> TextLines { get; set; } = new();
    }
}
