using FileConverter.Domain.Enums;

namespace FileConverter.Application;

public static class SupportedConversions
{
    // Standard raster output targets (Magick.NET can write all of these)
    private static readonly HashSet<FileFormat> RasterOutputs = new()
    {
        FileFormat.Png, FileFormat.Jpg, FileFormat.WebP, FileFormat.Gif,
        FileFormat.Bmp, FileFormat.Tiff, FileFormat.Ico, FileFormat.Avif,
        FileFormat.Tga, FileFormat.Jp2, FileFormat.Jfif, FileFormat.Dds
    };

    // Video output targets (FFmpeg)
    private static readonly HashSet<FileFormat> VideoOutputs = new()
    {
        FileFormat.Mp4, FileFormat.Mkv, FileFormat.WebM, FileFormat.Avi,
        FileFormat.Mov, FileFormat.Flv, FileFormat.Wmv, FileFormat.Ts
    };

    // Audio output targets (FFmpeg)
    private static readonly HashSet<FileFormat> AudioOutputs = new()
    {
        FileFormat.Mp3, FileFormat.Wav, FileFormat.Flac, FileFormat.Aac,
        FileFormat.Ogg, FileFormat.Wma, FileFormat.M4a, FileFormat.Opus
    };

    private static readonly Dictionary<FileFormat, HashSet<FileFormat>> ConversionMap = new()
    {
        // ═══════════════════════════════════════════
        // IMAGES — raster to raster + PDF (via Magick.NET)
        // ═══════════════════════════════════════════
        [FileFormat.Png]  = new(RasterOutputs) { FileFormat.Pdf },
        [FileFormat.Jpg]  = new(RasterOutputs) { FileFormat.Pdf },
        [FileFormat.WebP] = new(RasterOutputs) { FileFormat.Pdf },
        [FileFormat.Gif]  = new(RasterOutputs) { FileFormat.Pdf },
        [FileFormat.Bmp]  = new(RasterOutputs) { FileFormat.Pdf },
        [FileFormat.Tiff] = new(RasterOutputs) { FileFormat.Pdf },
        [FileFormat.Ico]  = new(RasterOutputs) { FileFormat.Pdf },
        [FileFormat.Avif] = new(RasterOutputs) { FileFormat.Pdf },
        [FileFormat.Psd]  = new(RasterOutputs) { FileFormat.Pdf },
        [FileFormat.Tga]  = new(RasterOutputs) { FileFormat.Pdf },
        [FileFormat.Jp2]  = new(RasterOutputs) { FileFormat.Pdf },
        [FileFormat.Jfif] = new(RasterOutputs) { FileFormat.Pdf },
        [FileFormat.Dds]  = new(RasterOutputs) { FileFormat.Pdf },

        // HEIC — read-only (patent blocks write), convert to any raster + PDF
        [FileFormat.Heic] = new(RasterOutputs) { FileFormat.Pdf },

        // RAW camera formats — read-only, convert to raster + PDF
        [FileFormat.Dng] = new(RasterOutputs) { FileFormat.Pdf },
        [FileFormat.Cr2] = new(RasterOutputs) { FileFormat.Pdf },
        [FileFormat.Nef] = new(RasterOutputs) { FileFormat.Pdf },
        [FileFormat.Arw] = new(RasterOutputs) { FileFormat.Pdf },

        // SVG — vector to raster + PDF (Svg.Skia)
        [FileFormat.Svg] = new() { FileFormat.Png, FileFormat.Jpg, FileFormat.WebP, FileFormat.Bmp, FileFormat.Tiff, FileFormat.Gif, FileFormat.Pdf },

        // ═══════════════════════════════════════════
        // DOCUMENTS — cross-convert via OpenXml/RtfPipe/HAP
        // ═══════════════════════════════════════════
        [FileFormat.Docx] = new() { FileFormat.Pdf, FileFormat.Odt, FileFormat.Rtf, FileFormat.Txt, FileFormat.Html },
        [FileFormat.Odt]  = new() { FileFormat.Pdf, FileFormat.Docx, FileFormat.Rtf, FileFormat.Txt, FileFormat.Html },
        [FileFormat.Rtf]  = new() { FileFormat.Pdf, FileFormat.Docx, FileFormat.Odt, FileFormat.Txt, FileFormat.Html },
        [FileFormat.Txt]  = new() { FileFormat.Pdf, FileFormat.Docx, FileFormat.Odt, FileFormat.Rtf, FileFormat.Html },
        [FileFormat.Html] = new() { FileFormat.Pdf, FileFormat.Docx, FileFormat.Odt, FileFormat.Rtf, FileFormat.Txt },

        // Markdown — via Markdig
        [FileFormat.Md] = new() { FileFormat.Html, FileFormat.Pdf, FileFormat.Txt, FileFormat.Docx },

        // ═══════════════════════════════════════════
        // PDF — to text + images
        // ═══════════════════════════════════════════
        [FileFormat.Pdf] = new() { FileFormat.Txt, FileFormat.Png, FileFormat.Jpg },

        // ═══════════════════════════════════════════
        // SPREADSHEETS — cross-convert
        // ═══════════════════════════════════════════
        [FileFormat.Xlsx] = new() { FileFormat.Pdf, FileFormat.Xls, FileFormat.Ods, FileFormat.Csv, FileFormat.Html, FileFormat.Txt },
        [FileFormat.Xls]  = new() { FileFormat.Pdf, FileFormat.Xlsx, FileFormat.Ods, FileFormat.Csv, FileFormat.Html, FileFormat.Txt },
        [FileFormat.Ods]  = new() { FileFormat.Pdf, FileFormat.Xlsx, FileFormat.Xls, FileFormat.Csv, FileFormat.Html, FileFormat.Txt },
        [FileFormat.Csv]  = new() { FileFormat.Pdf, FileFormat.Xlsx, FileFormat.Xls, FileFormat.Ods, FileFormat.Html },

        // ═══════════════════════════════════════════
        // PRESENTATIONS — cross-convert
        // ═══════════════════════════════════════════
        [FileFormat.Pptx] = new() { FileFormat.Pdf, FileFormat.Odp, FileFormat.Txt, FileFormat.Html },
        [FileFormat.Odp]  = new() { FileFormat.Pdf, FileFormat.Pptx, FileFormat.Txt, FileFormat.Html },

        // ═══════════════════════════════════════════
        // VIDEO — cross-convert + extract audio (FFmpeg)
        // ═══════════════════════════════════════════
        [FileFormat.Mp4]  = new(VideoOutputs) { FileFormat.Gif, FileFormat.Mp3, FileFormat.Wav, FileFormat.Aac },
        [FileFormat.Mkv]  = new(VideoOutputs) { FileFormat.Gif, FileFormat.Mp3, FileFormat.Wav, FileFormat.Aac },
        [FileFormat.WebM] = new(VideoOutputs) { FileFormat.Gif, FileFormat.Mp3, FileFormat.Wav, FileFormat.Aac },
        [FileFormat.Avi]  = new(VideoOutputs) { FileFormat.Gif, FileFormat.Mp3, FileFormat.Wav, FileFormat.Aac },
        [FileFormat.Mov]  = new(VideoOutputs) { FileFormat.Gif, FileFormat.Mp3, FileFormat.Wav, FileFormat.Aac },
        [FileFormat.Flv]  = new(VideoOutputs) { FileFormat.Gif, FileFormat.Mp3, FileFormat.Wav, FileFormat.Aac },
        [FileFormat.Wmv]  = new(VideoOutputs) { FileFormat.Gif, FileFormat.Mp3, FileFormat.Wav, FileFormat.Aac },
        [FileFormat.Ts]   = new(VideoOutputs) { FileFormat.Gif, FileFormat.Mp3, FileFormat.Wav, FileFormat.Aac },

        // ═══════════════════════════════════════════
        // AUDIO — cross-convert (FFmpeg)
        // ═══════════════════════════════════════════
        [FileFormat.Mp3]  = new(AudioOutputs),
        [FileFormat.Wav]  = new(AudioOutputs),
        [FileFormat.Flac] = new(AudioOutputs),
        [FileFormat.Aac]  = new(AudioOutputs),
        [FileFormat.Ogg]  = new(AudioOutputs),
        [FileFormat.Wma]  = new(AudioOutputs),
        [FileFormat.M4a]  = new(AudioOutputs),
        [FileFormat.Opus] = new(AudioOutputs),
    };

    // Remove self-conversions from sets that were initialized from RasterOutputs
    static SupportedConversions()
    {
        foreach (var (source, targets) in ConversionMap)
        {
            targets.Remove(source);
        }
    }

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
            "heic" or "heif" => FileFormat.Heic,
            "avif" => FileFormat.Avif,
            "psd" => FileFormat.Psd,
            "tga" => FileFormat.Tga,
            "jp2" or "j2k" or "jpeg2000" => FileFormat.Jp2,
            "jfif" => FileFormat.Jfif,
            "dds" => FileFormat.Dds,
            "dng" => FileFormat.Dng,
            "cr2" => FileFormat.Cr2,
            "nef" => FileFormat.Nef,
            "arw" => FileFormat.Arw,
            "pdf" => FileFormat.Pdf,
            "docx" => FileFormat.Docx,
            "doc" => FileFormat.Doc,
            "odt" => FileFormat.Odt,
            "rtf" => FileFormat.Rtf,
            "txt" or "text" => FileFormat.Txt,
            "html" or "htm" => FileFormat.Html,
            "md" or "markdown" => FileFormat.Md,
            "xlsx" => FileFormat.Xlsx,
            "xls" => FileFormat.Xls,
            "ods" => FileFormat.Ods,
            "csv" => FileFormat.Csv,
            "pptx" => FileFormat.Pptx,
            "ppt" => FileFormat.Ppt,
            "odp" => FileFormat.Odp,
            "mp4" => FileFormat.Mp4,
            "mkv" => FileFormat.Mkv,
            "webm" => FileFormat.WebM,
            "avi" => FileFormat.Avi,
            "mov" => FileFormat.Mov,
            "flv" => FileFormat.Flv,
            "wmv" => FileFormat.Wmv,
            "ts" or "mts" => FileFormat.Ts,
            "mp3" => FileFormat.Mp3,
            "wav" => FileFormat.Wav,
            "flac" => FileFormat.Flac,
            "aac" => FileFormat.Aac,
            "ogg" or "oga" => FileFormat.Ogg,
            "wma" => FileFormat.Wma,
            "m4a" => FileFormat.M4a,
            "opus" => FileFormat.Opus,
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
        FileFormat.Heic => ".heic",
        FileFormat.Avif => ".avif",
        FileFormat.Psd => ".psd",
        FileFormat.Tga => ".tga",
        FileFormat.Jp2 => ".jp2",
        FileFormat.Jfif => ".jfif",
        FileFormat.Dds => ".dds",
        FileFormat.Dng => ".dng",
        FileFormat.Cr2 => ".cr2",
        FileFormat.Nef => ".nef",
        FileFormat.Arw => ".arw",
        FileFormat.Pdf => ".pdf",
        FileFormat.Docx => ".docx",
        FileFormat.Doc => ".doc",
        FileFormat.Odt => ".odt",
        FileFormat.Rtf => ".rtf",
        FileFormat.Txt => ".txt",
        FileFormat.Html => ".html",
        FileFormat.Md => ".md",
        FileFormat.Xlsx => ".xlsx",
        FileFormat.Xls => ".xls",
        FileFormat.Ods => ".ods",
        FileFormat.Csv => ".csv",
        FileFormat.Pptx => ".pptx",
        FileFormat.Ppt => ".ppt",
        FileFormat.Odp => ".odp",
        FileFormat.Mp4 => ".mp4",
        FileFormat.Mkv => ".mkv",
        FileFormat.WebM => ".webm",
        FileFormat.Avi => ".avi",
        FileFormat.Mov => ".mov",
        FileFormat.Flv => ".flv",
        FileFormat.Wmv => ".wmv",
        FileFormat.Ts => ".ts",
        FileFormat.Mp3 => ".mp3",
        FileFormat.Wav => ".wav",
        FileFormat.Flac => ".flac",
        FileFormat.Aac => ".aac",
        FileFormat.Ogg => ".ogg",
        FileFormat.Wma => ".wma",
        FileFormat.M4a => ".m4a",
        FileFormat.Opus => ".opus",
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    public static FormatCategory GetCategory(FileFormat format) => format switch
    {
        FileFormat.Png or FileFormat.Jpg or FileFormat.WebP or FileFormat.Gif
            or FileFormat.Bmp or FileFormat.Tiff or FileFormat.Svg or FileFormat.Ico
            or FileFormat.Heic or FileFormat.Avif or FileFormat.Psd or FileFormat.Tga
            or FileFormat.Jp2 or FileFormat.Jfif or FileFormat.Dds
            or FileFormat.Dng or FileFormat.Cr2 or FileFormat.Nef or FileFormat.Arw => FormatCategory.Image,
        FileFormat.Xlsx or FileFormat.Xls or FileFormat.Ods or FileFormat.Csv => FormatCategory.Spreadsheet,
        FileFormat.Pptx or FileFormat.Ppt or FileFormat.Odp => FormatCategory.Presentation,
        FileFormat.Mp4 or FileFormat.Mkv or FileFormat.WebM or FileFormat.Avi
            or FileFormat.Mov or FileFormat.Flv or FileFormat.Wmv or FileFormat.Ts => FormatCategory.Video,
        FileFormat.Mp3 or FileFormat.Wav or FileFormat.Flac or FileFormat.Aac
            or FileFormat.Ogg or FileFormat.Wma or FileFormat.M4a or FileFormat.Opus => FormatCategory.Audio,
        _ => FormatCategory.Document
    };
}
