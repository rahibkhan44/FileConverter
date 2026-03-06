namespace FileConverter.Domain.Models;

public class ImageConversionOptions
{
    public int Quality { get; set; } = 85;
    public int? Width { get; set; }
    public int? Height { get; set; }
    public bool MaintainAspectRatio { get; set; } = true;
    public int Dpi { get; set; } = 72;
}

public class PdfConversionOptions
{
    public string PageSize { get; set; } = "A4";
    public string Orientation { get; set; } = "Portrait";
    public float MarginMm { get; set; } = 10;
    public float FontSize { get; set; } = 12;
}

public class SpreadsheetConversionOptions
{
    public int SheetIndex { get; set; } = 0;
    public string CsvDelimiter { get; set; } = ",";
    public bool IncludeHeaders { get; set; } = true;
}
