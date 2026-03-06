using ClosedXML.Excel;
using FileConverter.Application;
using FileConverter.Domain.Enums;
using FileConverter.Domain.Interfaces;
using UglyToad.PdfPig;
using System.Text;

namespace FileConverter.Infrastructure.Converters;

/// <summary>
/// Handles PDF text extraction (PDF → TXT) and text/spreadsheet → PDF.
/// PDF → DOCX/DOC/ODT/RTF/HTML is handled by NpoiDocumentConverter (extract text first, write to target).
/// Uses PdfPig for reading PDFs and shared PdfGenerationHelper for generating PDFs.
/// </summary>
public class PdfConverter : IFileConverter
{
    public bool CanConvert(FileFormat source, FileFormat target)
    {
        return (source, target) switch
        {
            (FileFormat.Txt, FileFormat.Pdf) => true,
            (FileFormat.Csv, FileFormat.Pdf) => true,
            (FileFormat.Xlsx, FileFormat.Pdf) => true,
            (FileFormat.Xls, FileFormat.Pdf) => true,
            (FileFormat.Pdf, FileFormat.Txt) => true,
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
        Directory.CreateDirectory(outputDirectory);

        if (sourceFormat == FileFormat.Pdf && targetFormat == FileFormat.Txt)
        {
            await ExtractPdfTextAsync(inputPath, outputPath, progress, cancellationToken);
        }
        else if (targetFormat == FileFormat.Pdf)
        {
            await ConvertToPdfAsync(inputPath, outputPath, sourceFormat, options, progress, cancellationToken);
        }

        progress?.Report(100);
        return outputPath;
    }

    private static async Task ExtractPdfTextAsync(string inputPath, string outputPath, IProgress<int>? progress, CancellationToken ct)
    {
        progress?.Report(20);

        using var document = PdfDocument.Open(inputPath);
        var sb = new StringBuilder();
        int totalPages = document.NumberOfPages;

        for (int i = 1; i <= totalPages; i++)
        {
            ct.ThrowIfCancellationRequested();
            var page = document.GetPage(i);
            sb.AppendLine(page.Text);
            if (totalPages > 1)
                sb.AppendLine($"\n--- Page {i} ---\n");
            progress?.Report(20 + (int)(70.0 * i / totalPages));
        }

        await File.WriteAllTextAsync(outputPath, sb.ToString(), ct);
    }

    private static async Task ConvertToPdfAsync(string inputPath, string outputPath, FileFormat sourceFormat,
        Dictionary<string, string> options, IProgress<int>? progress, CancellationToken ct)
    {
        progress?.Report(30);

        string content;
        if (sourceFormat == FileFormat.Csv)
        {
            content = await File.ReadAllTextAsync(inputPath, ct);
        }
        else if (sourceFormat is FileFormat.Xlsx or FileFormat.Xls)
        {
            content = ReadSpreadsheetAsText(inputPath);
        }
        else
        {
            content = await File.ReadAllTextAsync(inputPath, ct);
        }

        progress?.Report(60);
        PdfGenerationHelper.GeneratePdfFromText(content, outputPath, options);
    }

    private static string ReadSpreadsheetAsText(string inputPath)
    {
        using var workbook = new XLWorkbook(inputPath);
        var ws = workbook.Worksheets.First();
        var range = ws.RangeUsed();
        if (range == null) return "";

        var lines = new List<string>();
        for (int row = 1; row <= range.RowCount(); row++)
        {
            var cells = new List<string>();
            for (int col = 1; col <= range.ColumnCount(); col++)
                cells.Add(range.Cell(row, col).GetFormattedString());
            lines.Add(string.Join("\t", cells));
        }
        return string.Join(Environment.NewLine, lines);
    }
}
