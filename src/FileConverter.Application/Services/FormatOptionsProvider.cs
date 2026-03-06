using FileConverter.Application.DTOs;
using FileConverter.Domain.Enums;

namespace FileConverter.Application.Services;

public static class FormatOptionsProvider
{
    public static List<FormatOptionInfo> GetOptions(FileFormat sourceFormat, FileFormat targetFormat)
    {
        var sourceCategory = SupportedConversions.GetCategory(sourceFormat);
        var targetCategory = SupportedConversions.GetCategory(targetFormat);

        // Image target options
        if (targetCategory == FormatCategory.Image)
        {
            return GetImageOptions(targetFormat);
        }

        // PDF target options
        if (targetFormat == FileFormat.Pdf)
        {
            return GetPdfOptions(sourceCategory);
        }

        // Spreadsheet options
        if (targetFormat == FileFormat.Csv)
        {
            return GetCsvOptions();
        }

        if (targetFormat == FileFormat.Xlsx)
        {
            return GetXlsxOptions();
        }

        return new List<FormatOptionInfo>();
    }

    private static List<FormatOptionInfo> GetImageOptions(FileFormat targetFormat)
    {
        var options = new List<FormatOptionInfo>
        {
            new() { Name = "width", Type = "number", DefaultValue = "", Description = "Width in pixels (empty = original)" },
            new() { Name = "height", Type = "number", DefaultValue = "", Description = "Height in pixels (empty = original)" },
            new() { Name = "maintainAspectRatio", Type = "boolean", DefaultValue = "true", Description = "Maintain aspect ratio when resizing" },
            new() { Name = "dpi", Type = "number", DefaultValue = "72", Description = "DPI resolution", Min = 1, Max = 1200 },
        };

        if (targetFormat is FileFormat.Jpg or FileFormat.WebP)
        {
            options.Insert(0, new FormatOptionInfo
            {
                Name = "quality", Type = "number", DefaultValue = "85",
                Description = "Compression quality", Min = 1, Max = 100
            });
        }

        return options;
    }

    private static List<FormatOptionInfo> GetPdfOptions(FormatCategory sourceCategory)
    {
        if (sourceCategory == FormatCategory.Image)
        {
            return new List<FormatOptionInfo>
            {
                new() { Name = "pageSize", Type = "select", DefaultValue = "A4", Description = "Page size", AllowedValues = new() { "A4", "Letter", "Legal", "A3", "A5" } },
                new() { Name = "orientation", Type = "select", DefaultValue = "Portrait", Description = "Page orientation", AllowedValues = new() { "Portrait", "Landscape" } },
                new() { Name = "marginMm", Type = "number", DefaultValue = "10", Description = "Margin in mm", Min = 0, Max = 50 },
            };
        }

        return new List<FormatOptionInfo>
        {
            new() { Name = "pageSize", Type = "select", DefaultValue = "A4", Description = "Page size", AllowedValues = new() { "A4", "Letter", "Legal", "A3", "A5" } },
            new() { Name = "orientation", Type = "select", DefaultValue = "Portrait", Description = "Page orientation", AllowedValues = new() { "Portrait", "Landscape" } },
            new() { Name = "marginMm", Type = "number", DefaultValue = "10", Description = "Margin in mm", Min = 0, Max = 50 },
            new() { Name = "fontSize", Type = "number", DefaultValue = "12", Description = "Font size for text content", Min = 6, Max = 72 },
        };
    }

    private static List<FormatOptionInfo> GetCsvOptions()
    {
        return new List<FormatOptionInfo>
        {
            new() { Name = "delimiter", Type = "select", DefaultValue = ",", Description = "CSV delimiter", AllowedValues = new() { ",", ";", "\t", "|" } },
            new() { Name = "includeHeaders", Type = "boolean", DefaultValue = "true", Description = "Include column headers" },
            new() { Name = "sheetIndex", Type = "number", DefaultValue = "0", Description = "Sheet index (0-based)", Min = 0 },
        };
    }

    private static List<FormatOptionInfo> GetXlsxOptions()
    {
        return new List<FormatOptionInfo>
        {
            new() { Name = "sheetName", Type = "text", DefaultValue = "Sheet1", Description = "Name for the worksheet" },
            new() { Name = "includeHeaders", Type = "boolean", DefaultValue = "true", Description = "First row contains headers" },
        };
    }
}
