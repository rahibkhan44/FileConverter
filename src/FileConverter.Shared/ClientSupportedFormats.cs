using FileConverter.Domain.Enums;

namespace FileConverter.Shared;

public static class ClientSupportedFormats
{
    private static readonly Dictionary<string, List<string>> FormatMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // ═══ IMAGES — any raster to any raster + PDF + ICO ═══
        ["png"]  = new() { "jpg", "webp", "gif", "bmp", "tiff", "ico", "pdf", "svg" },
        ["jpg"]  = new() { "png", "webp", "gif", "bmp", "tiff", "ico", "pdf", "svg" },
        ["jpeg"] = new() { "png", "webp", "gif", "bmp", "tiff", "ico", "pdf", "svg" },
        ["webp"] = new() { "png", "jpg", "gif", "bmp", "tiff", "ico", "pdf" },
        ["gif"]  = new() { "png", "jpg", "webp", "bmp", "tiff", "ico", "pdf" },
        ["bmp"]  = new() { "png", "jpg", "webp", "gif", "tiff", "ico", "pdf" },
        ["tiff"] = new() { "png", "jpg", "webp", "gif", "bmp", "ico", "pdf" },
        ["tif"]  = new() { "png", "jpg", "webp", "gif", "bmp", "ico", "pdf" },
        ["ico"]  = new() { "png", "jpg", "webp", "gif", "bmp", "tiff" },
        ["svg"]  = new() { "png", "jpg", "webp", "bmp", "tiff", "gif", "pdf" },

        // ═══ DOCUMENTS — any doc to any doc via LibreOffice ═══
        ["docx"] = new() { "pdf", "doc", "odt", "rtf", "txt", "html" },
        ["doc"]  = new() { "pdf", "docx", "odt", "rtf", "txt", "html" },
        ["odt"]  = new() { "pdf", "docx", "doc", "rtf", "txt", "html" },
        ["rtf"]  = new() { "pdf", "docx", "doc", "odt", "txt", "html" },
        ["txt"]  = new() { "pdf", "docx", "doc", "odt", "rtf", "html" },
        ["html"] = new() { "pdf", "docx", "doc", "odt", "rtf", "txt" },
        ["htm"]  = new() { "pdf", "docx", "doc", "odt", "rtf", "txt" },

        // ═══ PDF — to documents + images ═══
        ["pdf"]  = new() { "txt", "docx", "doc", "odt", "rtf", "html", "png", "jpg" },

        // ═══ SPREADSHEETS — any spreadsheet to any spreadsheet ═══
        ["xlsx"] = new() { "pdf", "xls", "ods", "csv", "html", "txt" },
        ["xls"]  = new() { "pdf", "xlsx", "ods", "csv", "html", "txt" },
        ["ods"]  = new() { "pdf", "xlsx", "xls", "csv", "html", "txt" },
        ["csv"]  = new() { "pdf", "xlsx", "xls", "ods", "html" },

        // ═══ PRESENTATIONS — any presentation to any presentation ═══
        ["pptx"] = new() { "pdf", "ppt", "odp", "txt", "html" },
        ["ppt"]  = new() { "pdf", "pptx", "odp", "txt", "html" },
        ["odp"]  = new() { "pdf", "pptx", "ppt", "txt", "html" },
    };

    public static List<string> GetTargetFormats(string sourceExtension)
    {
        var ext = sourceExtension.TrimStart('.').ToLowerInvariant();
        return FormatMap.TryGetValue(ext, out var targets) ? targets : new List<string>();
    }

    public static bool IsSupported(string extension)
    {
        var ext = extension.TrimStart('.').ToLowerInvariant();
        return FormatMap.ContainsKey(ext);
    }

    public static IReadOnlyCollection<string> AllSupportedExtensions => FormatMap.Keys;

    public static string GetCategory(string extension)
    {
        var ext = extension.TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "png" or "jpg" or "jpeg" or "webp" or "gif" or "bmp" or "tiff" or "tif" or "svg" or "ico" => "Image",
            "xlsx" or "xls" or "ods" or "csv" => "Spreadsheet",
            "pptx" or "ppt" or "odp" => "Presentation",
            _ => "Document"
        };
    }
}
