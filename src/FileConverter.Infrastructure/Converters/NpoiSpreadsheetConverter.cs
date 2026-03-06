using ClosedXML.Excel;
using FileConverter.Application;
using FileConverter.Domain.Enums;
using FileConverter.Domain.Interfaces;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace FileConverter.Infrastructure.Converters;

/// <summary>
/// Handles all spreadsheet conversions: XLSX, XLS, ODS, CSV → any of the above + PDF/HTML/TXT.
/// Uses NPOI for XLS (binary), ClosedXML for XLSX, manual parsing for CSV/ODS.
/// Replaces LibreOfficeConverter for spreadsheet routes.
/// </summary>
public class NpoiSpreadsheetConverter : IFileConverter
{
    private static readonly HashSet<FileFormat> SpreadsheetSources = new()
    {
        FileFormat.Xlsx, FileFormat.Xls, FileFormat.Ods, FileFormat.Csv
    };

    private static readonly HashSet<FileFormat> ValidTargets = new()
    {
        FileFormat.Xlsx, FileFormat.Xls, FileFormat.Ods, FileFormat.Csv,
        FileFormat.Pdf, FileFormat.Html, FileFormat.Txt
    };

    public bool CanConvert(FileFormat source, FileFormat target)
        => SpreadsheetSources.Contains(source) && ValidTargets.Contains(target) && source != target;

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

        // Extract tabular data from source
        var data = await ReadSpreadsheetDataAsync(inputPath, sourceFormat, options, cancellationToken);
        progress?.Report(50);

        // Write to target
        await WriteSpreadsheetAsync(data, outputPath, targetFormat, options, cancellationToken);
        progress?.Report(100);

        return outputPath;
    }

    private static async Task<SpreadsheetData> ReadSpreadsheetDataAsync(
        string inputPath, FileFormat format, Dictionary<string, string> options, CancellationToken ct)
    {
        int sheetIndex = options.TryGetValue("sheetIndex", out var si) && int.TryParse(si, out var siv) ? siv : 0;

        return format switch
        {
            FileFormat.Xlsx => ReadXlsx(inputPath, sheetIndex),
            FileFormat.Xls => ReadXls(inputPath, sheetIndex),
            FileFormat.Ods => ReadOds(inputPath, sheetIndex),
            FileFormat.Csv => await ReadCsvAsync(inputPath, options, ct),
            _ => throw new NotSupportedException($"Cannot read {format} as spreadsheet")
        };
    }

    private static SpreadsheetData ReadXlsx(string inputPath, int sheetIndex)
    {
        using var workbook = new XLWorkbook(inputPath);
        var ws = sheetIndex < workbook.Worksheets.Count
            ? workbook.Worksheets.ElementAt(sheetIndex)
            : workbook.Worksheets.First();

        return ExtractClosedXmlData(ws);
    }

    private static SpreadsheetData ReadXls(string inputPath, int sheetIndex)
    {
        using var fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read);
        var workbook = new HSSFWorkbook(fs);
        var sheet = sheetIndex < workbook.NumberOfSheets
            ? workbook.GetSheetAt(sheetIndex)
            : workbook.GetSheetAt(0);

        return ExtractNpoiData(sheet);
    }

    private static SpreadsheetData ReadOds(string inputPath, int sheetIndex)
    {
        // ODS is a ZIP with content.xml containing table data
        using var zip = ZipFile.OpenRead(inputPath);
        var contentEntry = zip.GetEntry("content.xml");
        if (contentEntry == null) return new SpreadsheetData();

        using var stream = contentEntry.Open();
        var xdoc = XDocument.Load(stream);

        XNamespace tableNs = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
        XNamespace textNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

        var tables = xdoc.Descendants(tableNs + "table").ToList();
        var table = sheetIndex < tables.Count ? tables[sheetIndex] : tables.FirstOrDefault();
        if (table == null) return new SpreadsheetData();

        var rows = new List<List<string>>();
        foreach (var row in table.Descendants(tableNs + "table-row"))
        {
            var cells = new List<string>();
            foreach (var cell in row.Descendants(tableNs + "table-cell"))
            {
                var text = string.Join("", cell.Descendants(textNs + "p").Select(p => p.Value));
                // Handle table:number-columns-repeated
                var repeat = cell.Attribute(tableNs + "number-columns-repeated");
                int repeatCount = repeat != null && int.TryParse(repeat.Value, out var r) ? Math.Min(r, 100) : 1;
                for (int i = 0; i < repeatCount; i++)
                    cells.Add(text);
            }
            if (cells.Any(c => !string.IsNullOrEmpty(c)))
                rows.Add(cells);
        }

        return new SpreadsheetData { Rows = rows };
    }

    private static async Task<SpreadsheetData> ReadCsvAsync(string inputPath, Dictionary<string, string> options, CancellationToken ct)
    {
        var delimiter = options.TryGetValue("delimiter", out var d) && d.Length > 0 ? d[0] : ',';
        var lines = await File.ReadAllLinesAsync(inputPath, ct);
        var rows = lines
            .Select(line => ParseCsvLine(line, delimiter))
            .ToList();
        return new SpreadsheetData { Rows = rows };
    }

    private static async Task WriteSpreadsheetAsync(SpreadsheetData data, string outputPath, FileFormat target,
        Dictionary<string, string> options, CancellationToken ct)
    {
        switch (target)
        {
            case FileFormat.Xlsx:
                WriteXlsx(data, outputPath, options);
                break;
            case FileFormat.Xls:
                WriteXls(data, outputPath);
                break;
            case FileFormat.Csv:
                await WriteCsvAsync(data, outputPath, options, ct);
                break;
            case FileFormat.Ods:
                WriteOds(data, outputPath);
                break;
            case FileFormat.Pdf:
                WritePdf(data, outputPath, options);
                break;
            case FileFormat.Html:
                await WriteHtmlAsync(data, outputPath, ct);
                break;
            case FileFormat.Txt:
                await WriteTxtAsync(data, outputPath, ct);
                break;
        }
    }

    private static void WriteXlsx(SpreadsheetData data, string outputPath, Dictionary<string, string> options)
    {
        var sheetName = options.TryGetValue("sheetName", out var sn) ? sn : "Sheet1";
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add(sheetName);

        for (int r = 0; r < data.Rows.Count; r++)
        {
            for (int c = 0; c < data.Rows[r].Count; c++)
            {
                ws.Cell(r + 1, c + 1).Value = data.Rows[r][c];
            }
        }

        ws.Columns().AdjustToContents();
        workbook.SaveAs(outputPath);
    }

    private static void WriteXls(SpreadsheetData data, string outputPath)
    {
        var workbook = new HSSFWorkbook();
        var sheet = workbook.CreateSheet("Sheet1");

        for (int r = 0; r < data.Rows.Count; r++)
        {
            var row = sheet.CreateRow(r);
            for (int c = 0; c < data.Rows[r].Count; c++)
            {
                row.CreateCell(c).SetCellValue(data.Rows[r][c]);
            }
        }

        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        workbook.Write(fs);
    }

    private static async Task WriteCsvAsync(SpreadsheetData data, string outputPath, Dictionary<string, string> options, CancellationToken ct)
    {
        var delimiter = options.TryGetValue("delimiter", out var d) ? d : ",";
        bool includeHeaders = !options.TryGetValue("includeHeaders", out var ih) || !bool.TryParse(ih, out var ihv) || ihv;

        using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
        int startRow = includeHeaders ? 0 : 1;

        for (int r = startRow; r < data.Rows.Count; r++)
        {
            ct.ThrowIfCancellationRequested();
            var cells = data.Rows[r].Select(cell =>
            {
                if (cell.Contains(delimiter) || cell.Contains('"') || cell.Contains('\n'))
                    return $"\"{cell.Replace("\"", "\"\"")}\"";
                return cell;
            });
            await writer.WriteLineAsync(string.Join(delimiter, cells));
        }
    }

    private static void WriteOds(SpreadsheetData data, string outputPath)
    {
        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

        var mimeEntry = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
        using (var writer = new StreamWriter(mimeEntry.Open()))
        {
            writer.Write("application/vnd.oasis.opendocument.spreadsheet");
        }

        XNamespace officeNs = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
        XNamespace tableNs = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
        XNamespace textNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

        var rows = data.Rows.Select(row =>
            new XElement(tableNs + "table-row",
                row.Select(cell =>
                    new XElement(tableNs + "table-cell",
                        new XElement(textNs + "p", cell)
                    )
                )
            )
        );

        var contentXml = new XDocument(
            new XElement(officeNs + "document-content",
                new XAttribute(XNamespace.Xmlns + "office", officeNs),
                new XAttribute(XNamespace.Xmlns + "table", tableNs),
                new XAttribute(XNamespace.Xmlns + "text", textNs),
                new XAttribute(officeNs + "version", "1.2"),
                new XElement(officeNs + "body",
                    new XElement(officeNs + "spreadsheet",
                        new XElement(tableNs + "table",
                            new XAttribute(tableNs + "name", "Sheet1"),
                            rows
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

        XNamespace manifestNs = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";
        var manifestXml = new XDocument(
            new XElement(manifestNs + "manifest",
                new XAttribute(XNamespace.Xmlns + "manifest", manifestNs),
                new XElement(manifestNs + "file-entry",
                    new XAttribute(manifestNs + "media-type", "application/vnd.oasis.opendocument.spreadsheet"),
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

    private static void WritePdf(SpreadsheetData data, string outputPath, Dictionary<string, string> options)
    {
        // Format spreadsheet as tab-delimited text for PDF
        var sb = new StringBuilder();
        foreach (var row in data.Rows)
        {
            sb.AppendLine(string.Join("\t", row));
        }
        PdfGenerationHelper.GeneratePdfFromText(sb.ToString(), outputPath, options);
    }

    private static async Task WriteHtmlAsync(SpreadsheetData data, string outputPath, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><meta charset=\"utf-8\"><title>Spreadsheet</title>");
        sb.AppendLine("<style>table{border-collapse:collapse;width:100%}td,th{border:1px solid #ddd;padding:8px;text-align:left}tr:nth-child(even){background-color:#f2f2f2}th{background-color:#4CAF50;color:white}</style>");
        sb.AppendLine("</head><body><table>");

        for (int r = 0; r < data.Rows.Count; r++)
        {
            var tag = r == 0 ? "th" : "td";
            sb.Append("<tr>");
            foreach (var cell in data.Rows[r])
            {
                sb.Append($"<{tag}>{System.Net.WebUtility.HtmlEncode(cell)}</{tag}>");
            }
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</table></body></html>");
        await File.WriteAllTextAsync(outputPath, sb.ToString(), ct);
    }

    private static async Task WriteTxtAsync(SpreadsheetData data, string outputPath, CancellationToken ct)
    {
        var sb = new StringBuilder();
        foreach (var row in data.Rows)
        {
            sb.AppendLine(string.Join("\t", row));
        }
        await File.WriteAllTextAsync(outputPath, sb.ToString(), ct);
    }

    // -- Helpers --

    private static SpreadsheetData ExtractClosedXmlData(IXLWorksheet ws)
    {
        var range = ws.RangeUsed();
        if (range == null) return new SpreadsheetData();

        var rows = new List<List<string>>();
        for (int r = 1; r <= range.RowCount(); r++)
        {
            var row = new List<string>();
            for (int c = 1; c <= range.ColumnCount(); c++)
            {
                row.Add(range.Cell(r, c).GetFormattedString());
            }
            rows.Add(row);
        }
        return new SpreadsheetData { Rows = rows };
    }

    private static SpreadsheetData ExtractNpoiData(ISheet sheet)
    {
        var rows = new List<List<string>>();
        for (int r = sheet.FirstRowNum; r <= sheet.LastRowNum; r++)
        {
            var row = sheet.GetRow(r);
            if (row == null) { rows.Add(new List<string>()); continue; }

            var cells = new List<string>();
            for (int c = 0; c < row.LastCellNum; c++)
            {
                var cell = row.GetCell(c);
                cells.Add(cell?.ToString() ?? "");
            }
            rows.Add(cells);
        }
        return new SpreadsheetData { Rows = rows };
    }

    private static List<string> ParseCsvLine(string line, char delimiter)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }

    private class SpreadsheetData
    {
        public List<List<string>> Rows { get; set; } = new();
    }
}
