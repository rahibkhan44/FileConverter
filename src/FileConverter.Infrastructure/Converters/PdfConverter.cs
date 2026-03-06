using FileConverter.Application;
using FileConverter.Domain.Enums;
using FileConverter.Domain.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace FileConverter.Infrastructure.Converters;

public class PdfConverter : IFileConverter
{
    public bool CanConvert(FileFormat source, FileFormat target)
    {
        return (source, target) switch
        {
            (FileFormat.Txt, FileFormat.Pdf) => true,
            (FileFormat.Csv, FileFormat.Pdf) => true,
            (FileFormat.Pdf, FileFormat.Txt) => true,
            (FileFormat.Xlsx, FileFormat.Pdf) => true,
            (FileFormat.Xls, FileFormat.Pdf) => true,
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

        if (targetFormat == FileFormat.Txt && sourceFormat == FileFormat.Pdf)
        {
            await ExtractPdfTextAsync(inputPath, outputPath, progress, cancellationToken);
        }
        else if (targetFormat == FileFormat.Pdf)
        {
            await ConvertToPdfAsync(inputPath, outputPath, sourceFormat, options, progress, cancellationToken);
        }

        return outputPath;
    }

    private async Task ExtractPdfTextAsync(string inputPath, string outputPath, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(20);

        using var document = PdfDocument.Open(inputPath);
        var textLines = new List<string>();
        int totalPages = document.NumberOfPages;

        for (int i = 1; i <= totalPages; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = document.GetPage(i);
            textLines.Add(page.Text);
            textLines.Add(""); // Page separator
            progress?.Report(20 + (int)(70.0 * i / totalPages));
        }

        await File.WriteAllTextAsync(outputPath, string.Join(Environment.NewLine, textLines), cancellationToken);
        progress?.Report(100);
    }

    private async Task ConvertToPdfAsync(string inputPath, string outputPath, FileFormat sourceFormat,
        Dictionary<string, string> options, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        var pageSize = GetPageSize(options);
        var orientation = options.TryGetValue("orientation", out var o) && o.Equals("Landscape", StringComparison.OrdinalIgnoreCase);
        float margin = options.TryGetValue("marginMm", out var m) && float.TryParse(m, out var mv) ? mv : 10;
        float fontSize = options.TryGetValue("fontSize", out var fs) && float.TryParse(fs, out var fsv) ? fsv : 12;

        if (orientation) pageSize = new QuestPDF.Helpers.PageSize(pageSize.Height, pageSize.Width);

        progress?.Report(30);

        string content;
        if (sourceFormat == FileFormat.Csv)
        {
            content = await File.ReadAllTextAsync(inputPath, cancellationToken);
        }
        else if (sourceFormat is FileFormat.Xlsx or FileFormat.Xls)
        {
            content = ReadSpreadsheetAsText(inputPath);
        }
        else
        {
            content = await File.ReadAllTextAsync(inputPath, cancellationToken);
        }

        progress?.Report(60);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(pageSize);
                page.Margin(margin, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontSize(fontSize));

                page.Content().Text(content);
            });
        }).GeneratePdf(outputPath);

        progress?.Report(100);
    }

    private static string ReadSpreadsheetAsText(string inputPath)
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook(inputPath);
        var ws = workbook.Worksheets.First();
        var range = ws.RangeUsed();
        if (range == null) return "";

        var lines = new List<string>();
        for (int row = 1; row <= range.RowCount(); row++)
        {
            var cells = new List<string>();
            for (int col = 1; col <= range.ColumnCount(); col++)
            {
                cells.Add(range.Cell(row, col).GetFormattedString());
            }
            lines.Add(string.Join("\t", cells));
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static QuestPDF.Helpers.PageSize GetPageSize(Dictionary<string, string> options)
    {
        var size = options.TryGetValue("pageSize", out var ps) ? ps : "A4";
        return size.ToUpperInvariant() switch
        {
            "A3" => PageSizes.A3,
            "A4" => PageSizes.A4,
            "A5" => PageSizes.A5,
            "LETTER" => PageSizes.Letter,
            "LEGAL" => PageSizes.Legal,
            _ => PageSizes.A4
        };
    }
}
