using ClosedXML.Excel;
using FileConverter.Application;
using FileConverter.Domain.Enums;
using FileConverter.Domain.Interfaces;

namespace FileConverter.Infrastructure.Converters;

public class SpreadsheetConverter : IFileConverter
{
    public bool CanConvert(FileFormat source, FileFormat target)
    {
        return (source, target) switch
        {
            (FileFormat.Xlsx, FileFormat.Csv) => true,
            (FileFormat.Xls, FileFormat.Csv) => true,
            (FileFormat.Csv, FileFormat.Xlsx) => true,
            (FileFormat.Xls, FileFormat.Xlsx) => true,
            _ => false
        };
    }

    public async Task<string> ConvertAsync(string inputPath, string outputDirectory, FileFormat targetFormat,
        Dictionary<string, string> options, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(10);

        var outputFileName = Path.GetFileNameWithoutExtension(inputPath) + SupportedConversions.GetExtension(targetFormat);
        var outputPath = Path.Combine(outputDirectory, outputFileName);

        if (targetFormat == FileFormat.Csv)
        {
            await ConvertToCsvAsync(inputPath, outputPath, options, progress, cancellationToken);
        }
        else if (targetFormat == FileFormat.Xlsx)
        {
            await ConvertToXlsxAsync(inputPath, outputPath, options, progress, cancellationToken);
        }

        progress?.Report(100);
        return outputPath;
    }

    private async Task ConvertToCsvAsync(string inputPath, string outputPath, Dictionary<string, string> options,
        IProgress<int>? progress, CancellationToken cancellationToken)
    {
        var delimiter = options.TryGetValue("delimiter", out var d) ? d : ",";
        int sheetIndex = options.TryGetValue("sheetIndex", out var si) && int.TryParse(si, out var siv) ? siv : 0;
        bool includeHeaders = !options.TryGetValue("includeHeaders", out var ih) || !bool.TryParse(ih, out var ihv) || ihv;

        progress?.Report(30);

        using var workbook = new XLWorkbook(inputPath);
        var worksheet = workbook.Worksheets.Count > sheetIndex
            ? workbook.Worksheets.ElementAt(sheetIndex)
            : workbook.Worksheets.First();

        progress?.Report(50);

        var range = worksheet.RangeUsed();
        if (range == null)
        {
            await File.WriteAllTextAsync(outputPath, "", cancellationToken);
            return;
        }

        using var writer = new StreamWriter(outputPath);
        int startRow = includeHeaders ? 1 : 2;

        for (int row = 1; row <= range.RowCount(); row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cells = new List<string>();
            for (int col = 1; col <= range.ColumnCount(); col++)
            {
                var cellValue = range.Cell(row, col).GetFormattedString();
                if (cellValue.Contains(delimiter) || cellValue.Contains('"') || cellValue.Contains('\n'))
                    cellValue = $"\"{cellValue.Replace("\"", "\"\"")}\"";
                cells.Add(cellValue);
            }
            await writer.WriteLineAsync(string.Join(delimiter, cells));

            if (range.RowCount() > 0)
                progress?.Report(50 + (int)(40.0 * row / range.RowCount()));
        }
    }

    private async Task ConvertToXlsxAsync(string inputPath, string outputPath, Dictionary<string, string> options,
        IProgress<int>? progress, CancellationToken cancellationToken)
    {
        var sheetName = options.TryGetValue("sheetName", out var sn) ? sn : "Sheet1";
        bool includeHeaders = !options.TryGetValue("includeHeaders", out var ih) || !bool.TryParse(ih, out var ihv) || ihv;

        progress?.Report(30);

        // If source is CSV
        if (Path.GetExtension(inputPath).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            var delimiter = options.TryGetValue("delimiter", out var d) ? d : ",";
            var lines = await File.ReadAllLinesAsync(inputPath, cancellationToken);

            progress?.Report(50);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(sheetName);

            for (int i = 0; i < lines.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var values = ParseCsvLine(lines[i], delimiter[0]);
                for (int j = 0; j < values.Length; j++)
                {
                    worksheet.Cell(i + 1, j + 1).Value = values[j];
                }
            }

            worksheet.Columns().AdjustToContents();
            workbook.SaveAs(outputPath);
        }
        else
        {
            // XLS to XLSX — ClosedXML can read XLS too
            using var workbook = new XLWorkbook(inputPath);
            workbook.SaveAs(outputPath);
        }

        progress?.Report(90);
    }

    private static string[] ParseCsvLine(string line, char delimiter)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

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
        return result.ToArray();
    }
}
