namespace FileConverter.Shared;

public static class ClientSupportedFormats
{
    private static readonly string[] RasterOutputs = { "png", "jpg", "webp", "gif", "bmp", "tiff", "ico", "avif", "tga", "jp2", "jfif", "dds" };
    private static readonly string[] VideoOutputs = { "mp4", "mkv", "webm", "avi", "mov", "flv", "wmv", "ts" };
    private static readonly string[] AudioOutputs = { "mp3", "wav", "flac", "aac", "ogg", "wma", "m4a", "opus" };

    private static readonly Dictionary<string, List<string>> FormatMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // ═══ IMAGES — raster to raster + PDF (via Magick.NET) ═══
        ["png"]  = new(RasterOutputs) { "pdf" },
        ["jpg"]  = new(RasterOutputs) { "pdf" },
        ["jpeg"] = new(RasterOutputs) { "pdf" },
        ["webp"] = new(RasterOutputs) { "pdf" },
        ["gif"]  = new(RasterOutputs) { "pdf" },
        ["bmp"]  = new(RasterOutputs) { "pdf" },
        ["tiff"] = new(RasterOutputs) { "pdf" },
        ["tif"]  = new(RasterOutputs) { "pdf" },
        ["ico"]  = new(RasterOutputs) { "pdf" },
        ["avif"] = new(RasterOutputs) { "pdf" },
        ["psd"]  = new(RasterOutputs) { "pdf" },
        ["tga"]  = new(RasterOutputs) { "pdf" },
        ["jp2"]  = new(RasterOutputs) { "pdf" },
        ["jfif"] = new(RasterOutputs) { "pdf" },
        ["dds"]  = new(RasterOutputs) { "pdf" },

        // HEIC — read-only (patent), convert to raster + PDF
        ["heic"] = new(RasterOutputs) { "pdf" },
        ["heif"] = new(RasterOutputs) { "pdf" },

        // RAW camera — read-only, convert to raster + PDF
        ["dng"] = new(RasterOutputs) { "pdf" },
        ["cr2"] = new(RasterOutputs) { "pdf" },
        ["nef"] = new(RasterOutputs) { "pdf" },
        ["arw"] = new(RasterOutputs) { "pdf" },

        // SVG — vector to raster + PDF
        ["svg"] = new() { "png", "jpg", "webp", "bmp", "tiff", "gif", "pdf" },

        // ═══ DOCUMENTS ═══
        ["docx"] = new() { "pdf", "odt", "rtf", "txt", "html" },
        ["odt"]  = new() { "pdf", "docx", "rtf", "txt", "html" },
        ["rtf"]  = new() { "pdf", "docx", "odt", "txt", "html" },
        ["txt"]  = new() { "pdf", "docx", "odt", "rtf", "html" },
        ["html"] = new() { "pdf", "docx", "odt", "rtf", "txt" },
        ["htm"]  = new() { "pdf", "docx", "odt", "rtf", "txt" },

        // Markdown
        ["md"]       = new() { "html", "pdf", "txt", "docx" },
        ["markdown"] = new() { "html", "pdf", "txt", "docx" },

        // ═══ PDF ═══
        ["pdf"] = new() { "txt", "png", "jpg" },

        // ═══ SPREADSHEETS ═══
        ["xlsx"] = new() { "pdf", "xls", "ods", "csv", "html", "txt" },
        ["xls"]  = new() { "pdf", "xlsx", "ods", "csv", "html", "txt" },
        ["ods"]  = new() { "pdf", "xlsx", "xls", "csv", "html", "txt" },
        ["csv"]  = new() { "pdf", "xlsx", "xls", "ods", "html" },

        // ═══ PRESENTATIONS ═══
        ["pptx"] = new() { "pdf", "odp", "txt", "html" },
        ["odp"]  = new() { "pdf", "pptx", "txt", "html" },

        // ═══ VIDEO — cross-convert + audio extraction (FFmpeg) ═══
        ["mp4"]  = new(VideoOutputs) { "gif", "mp3", "wav", "aac" },
        ["mkv"]  = new(VideoOutputs) { "gif", "mp3", "wav", "aac" },
        ["webm"] = new(VideoOutputs) { "gif", "mp3", "wav", "aac" },
        ["avi"]  = new(VideoOutputs) { "gif", "mp3", "wav", "aac" },
        ["mov"]  = new(VideoOutputs) { "gif", "mp3", "wav", "aac" },
        ["flv"]  = new(VideoOutputs) { "gif", "mp3", "wav", "aac" },
        ["wmv"]  = new(VideoOutputs) { "gif", "mp3", "wav", "aac" },
        ["ts"]   = new(VideoOutputs) { "gif", "mp3", "wav", "aac" },

        // ═══ AUDIO — cross-convert (FFmpeg) ═══
        ["mp3"]  = new(AudioOutputs),
        ["wav"]  = new(AudioOutputs),
        ["flac"] = new(AudioOutputs),
        ["aac"]  = new(AudioOutputs),
        ["ogg"]  = new(AudioOutputs),
        ["wma"]  = new(AudioOutputs),
        ["m4a"]  = new(AudioOutputs),
        ["opus"] = new(AudioOutputs),
    };

    // Remove self from targets (e.g., png shouldn't list png as target)
    static ClientSupportedFormats()
    {
        foreach (var (ext, targets) in FormatMap)
        {
            targets.Remove(ext);
        }
    }

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

    public static IReadOnlyDictionary<string, List<string>> GetAll() => FormatMap;

    public static string GetCategory(string extension)
    {
        var ext = extension.TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "png" or "jpg" or "jpeg" or "webp" or "gif" or "bmp" or "tiff" or "tif"
                or "svg" or "ico" or "heic" or "heif" or "avif" or "psd" or "tga"
                or "jp2" or "jfif" or "dds" or "dng" or "cr2" or "nef" or "arw" => "Image",
            "xlsx" or "xls" or "ods" or "csv" => "Spreadsheet",
            "pptx" or "odp" => "Presentation",
            "mp4" or "mkv" or "webm" or "avi" or "mov" or "flv" or "wmv" or "ts" => "Video",
            "mp3" or "wav" or "flac" or "aac" or "ogg" or "wma" or "m4a" or "opus" => "Audio",
            _ => "Document"
        };
    }

    public static string GetFriendlyName(string extension)
    {
        var ext = extension.TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "png" => "PNG Image", "jpg" or "jpeg" => "JPEG Image", "webp" => "WebP Image",
            "gif" => "GIF Image", "bmp" => "Bitmap", "tiff" or "tif" => "TIFF Image",
            "svg" => "SVG Vector", "ico" => "Icon", "heic" or "heif" => "HEIC Image",
            "avif" => "AVIF Image", "psd" => "Photoshop", "tga" => "Targa Image",
            "jp2" => "JPEG 2000", "jfif" => "JFIF Image", "dds" => "DirectDraw Surface",
            "dng" => "DNG RAW", "cr2" => "Canon RAW", "nef" => "Nikon RAW", "arw" => "Sony RAW",
            "pdf" => "PDF Document", "docx" => "Word Document", "odt" => "OpenDocument Text",
            "rtf" => "Rich Text", "txt" => "Plain Text", "html" or "htm" => "HTML Page",
            "md" or "markdown" => "Markdown",
            "xlsx" => "Excel Spreadsheet", "xls" => "Excel (Legacy)", "ods" => "OpenDocument Sheet",
            "csv" => "CSV Data", "pptx" => "PowerPoint", "odp" => "OpenDocument Presentation",
            "mp4" => "MP4 Video", "mkv" => "MKV Video", "webm" => "WebM Video",
            "avi" => "AVI Video", "mov" => "QuickTime Video", "flv" => "Flash Video",
            "wmv" => "Windows Media Video", "ts" => "MPEG Transport Stream",
            "mp3" => "MP3 Audio", "wav" => "WAV Audio", "flac" => "FLAC Audio",
            "aac" => "AAC Audio", "ogg" => "OGG Audio", "wma" => "Windows Media Audio",
            "m4a" => "M4A Audio", "opus" => "Opus Audio",
            _ => ext.ToUpperInvariant()
        };
    }
}
