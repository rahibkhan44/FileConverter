using FileConverter.Domain.Enums;

namespace FileConverter.Application;

public static class SupportedConversions
{
    private static readonly Dictionary<FileFormat, HashSet<FileFormat>> ConversionMap = new()
    {
        // ═══════════════════════════════════════════
        // IMAGES — any raster to any raster + PDF + ICO
        // ═══════════════════════════════════════════
        [FileFormat.Png]  = new() { FileFormat.Jpg, FileFormat.WebP, FileFormat.Gif, FileFormat.Bmp, FileFormat.Tiff, FileFormat.Ico, FileFormat.Pdf, FileFormat.Svg },
        [FileFormat.Jpg]  = new() { FileFormat.Png, FileFormat.WebP, FileFormat.Gif, FileFormat.Bmp, FileFormat.Tiff, FileFormat.Ico, FileFormat.Pdf, FileFormat.Svg },
        [FileFormat.WebP] = new() { FileFormat.Png, FileFormat.Jpg, FileFormat.Gif, FileFormat.Bmp, FileFormat.Tiff, FileFormat.Ico, FileFormat.Pdf },
        [FileFormat.Gif]  = new() { FileFormat.Png, FileFormat.Jpg, FileFormat.WebP, FileFormat.Bmp, FileFormat.Tiff, FileFormat.Ico, FileFormat.Pdf },
        [FileFormat.Bmp]  = new() { FileFormat.Png, FileFormat.Jpg, FileFormat.WebP, FileFormat.Gif, FileFormat.Tiff, FileFormat.Ico, FileFormat.Pdf },
        [FileFormat.Tiff] = new() { FileFormat.Png, FileFormat.Jpg, FileFormat.WebP, FileFormat.Gif, FileFormat.Bmp, FileFormat.Ico, FileFormat.Pdf },
        [FileFormat.Ico]  = new() { FileFormat.Png, FileFormat.Jpg, FileFormat.WebP, FileFormat.Gif, FileFormat.Bmp, FileFormat.Tiff },
        [FileFormat.Svg]  = new() { FileFormat.Png, FileFormat.Jpg, FileFormat.WebP, FileFormat.Bmp, FileFormat.Tiff, FileFormat.Gif, FileFormat.Pdf },

        // ═══════════════════════════════════════════
        // DOCUMENTS — any doc to any doc via LibreOffice
        // ═══════════════════════════════════════════
        [FileFormat.Docx] = new() { FileFormat.Pdf, FileFormat.Doc, FileFormat.Odt, FileFormat.Rtf, FileFormat.Txt, FileFormat.Html },
        [FileFormat.Doc]  = new() { FileFormat.Pdf, FileFormat.Docx, FileFormat.Odt, FileFormat.Rtf, FileFormat.Txt, FileFormat.Html },
        [FileFormat.Odt]  = new() { FileFormat.Pdf, FileFormat.Docx, FileFormat.Doc, FileFormat.Rtf, FileFormat.Txt, FileFormat.Html },
        [FileFormat.Rtf]  = new() { FileFormat.Pdf, FileFormat.Docx, FileFormat.Doc, FileFormat.Odt, FileFormat.Txt, FileFormat.Html },
        [FileFormat.Txt]  = new() { FileFormat.Pdf, FileFormat.Docx, FileFormat.Doc, FileFormat.Odt, FileFormat.Rtf, FileFormat.Html },
        [FileFormat.Html] = new() { FileFormat.Pdf, FileFormat.Docx, FileFormat.Doc, FileFormat.Odt, FileFormat.Rtf, FileFormat.Txt },

        // ═══════════════════════════════════════════
        // PDF — to documents + images
        // ═══════════════════════════════════════════
        [FileFormat.Pdf]  = new() { FileFormat.Txt, FileFormat.Docx, FileFormat.Doc, FileFormat.Odt, FileFormat.Rtf, FileFormat.Html,
                                     FileFormat.Png, FileFormat.Jpg },

        // ═══════════════════════════════════════════
        // SPREADSHEETS — any spreadsheet to any spreadsheet
        // ═══════════════════════════════════════════
        [FileFormat.Xlsx] = new() { FileFormat.Pdf, FileFormat.Xls, FileFormat.Ods, FileFormat.Csv, FileFormat.Html, FileFormat.Txt },
        [FileFormat.Xls]  = new() { FileFormat.Pdf, FileFormat.Xlsx, FileFormat.Ods, FileFormat.Csv, FileFormat.Html, FileFormat.Txt },
        [FileFormat.Ods]  = new() { FileFormat.Pdf, FileFormat.Xlsx, FileFormat.Xls, FileFormat.Csv, FileFormat.Html, FileFormat.Txt },
        [FileFormat.Csv]  = new() { FileFormat.Pdf, FileFormat.Xlsx, FileFormat.Xls, FileFormat.Ods, FileFormat.Html },

        // ═══════════════════════════════════════════
        // PRESENTATIONS — any presentation to any presentation
        // ═══════════════════════════════════════════
        [FileFormat.Pptx] = new() { FileFormat.Pdf, FileFormat.Ppt, FileFormat.Odp, FileFormat.Txt, FileFormat.Html },
        [FileFormat.Ppt]  = new() { FileFormat.Pdf, FileFormat.Pptx, FileFormat.Odp, FileFormat.Txt, FileFormat.Html },
        [FileFormat.Odp]  = new() { FileFormat.Pdf, FileFormat.Pptx, FileFormat.Ppt, FileFormat.Txt, FileFormat.Html },
    };

    public static bool IsSupported(FileFormat source, FileFormat target)
        => ConversionMap.TryGetValue(source, out var targets) && targets.Contains(target);

    public static IReadOnlySet<FileFormat> GetTargets(FileFormat source)
        => ConversionMap.TryGetValue(source, out var targets) ? targets : new HashSet<FileFormat>();

    public static IReadOnlyDictionary<FileFormat, HashSet<FileFormat>> GetAll() => ConversionMap;

    public static FileFormat? ParseFormat(string extension)
    {
        var ext = extension.TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "png" => FileFormat.Png,
            "jpg" or "jpeg" => FileFormat.Jpg,
            "webp" => FileFormat.WebP,
            "gif" => FileFormat.Gif,
            "bmp" => FileFormat.Bmp,
            "tiff" or "tif" => FileFormat.Tiff,
            "svg" => FileFormat.Svg,
            "ico" => FileFormat.Ico,
            "pdf" => FileFormat.Pdf,
            "docx" => FileFormat.Docx,
            "doc" => FileFormat.Doc,
            "odt" => FileFormat.Odt,
            "rtf" => FileFormat.Rtf,
            "txt" or "text" => FileFormat.Txt,
            "html" or "htm" => FileFormat.Html,
            "xlsx" => FileFormat.Xlsx,
            "xls" => FileFormat.Xls,
            "ods" => FileFormat.Ods,
            "csv" => FileFormat.Csv,
            "pptx" => FileFormat.Pptx,
            "ppt" => FileFormat.Ppt,
            "odp" => FileFormat.Odp,
            _ => null
        };
    }

    public static string GetExtension(FileFormat format) => format switch
    {
        FileFormat.Png => ".png",
        FileFormat.Jpg => ".jpg",
        FileFormat.WebP => ".webp",
        FileFormat.Gif => ".gif",
        FileFormat.Bmp => ".bmp",
        FileFormat.Tiff => ".tiff",
        FileFormat.Svg => ".svg",
        FileFormat.Ico => ".ico",
        FileFormat.Pdf => ".pdf",
        FileFormat.Docx => ".docx",
        FileFormat.Doc => ".doc",
        FileFormat.Odt => ".odt",
        FileFormat.Rtf => ".rtf",
        FileFormat.Txt => ".txt",
        FileFormat.Html => ".html",
        FileFormat.Xlsx => ".xlsx",
        FileFormat.Xls => ".xls",
        FileFormat.Ods => ".ods",
        FileFormat.Csv => ".csv",
        FileFormat.Pptx => ".pptx",
        FileFormat.Ppt => ".ppt",
        FileFormat.Odp => ".odp",
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    public static FormatCategory GetCategory(FileFormat format) => format switch
    {
        FileFormat.Png or FileFormat.Jpg or FileFormat.WebP or FileFormat.Gif
            or FileFormat.Bmp or FileFormat.Tiff or FileFormat.Svg or FileFormat.Ico => FormatCategory.Image,
        FileFormat.Xlsx or FileFormat.Xls or FileFormat.Ods or FileFormat.Csv => FormatCategory.Spreadsheet,
        FileFormat.Pptx or FileFormat.Ppt or FileFormat.Odp => FormatCategory.Presentation,
        _ => FormatCategory.Document
    };
}
