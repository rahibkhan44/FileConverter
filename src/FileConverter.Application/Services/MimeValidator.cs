using Microsoft.Extensions.Logging;

namespace FileConverter.Application.Services;

/// <summary>
/// Validates uploaded files by checking magic bytes (file signatures) against declared file extensions.
/// Prevents malicious uploads disguised with incorrect extensions.
/// </summary>
public static class MimeValidator
{
    // Text-based formats that cannot be validated via magic bytes
    private static readonly HashSet<string> TextBasedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".csv", ".tsv", ".html", ".htm", ".md", ".svg", ".xml", ".json", ".css", ".js", ".yaml", ".yml"
    };

    // Magic byte signatures: (offset, signature bytes, detected MIME type, matching extensions)
    private static readonly List<MagicSignature> Signatures = new()
    {
        // PDF: %PDF
        new(0, new byte[] { 0x25, 0x50, 0x44, 0x46 }, "application/pdf", new[] { ".pdf" }),

        // PNG: 89 50 4E 47
        new(0, new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "image/png", new[] { ".png" }),

        // JPEG: FF D8 FF
        new(0, new byte[] { 0xFF, 0xD8, 0xFF }, "image/jpeg", new[] { ".jpg", ".jpeg", ".jfif" }),

        // GIF: GIF8
        new(0, new byte[] { 0x47, 0x49, 0x46, 0x38 }, "image/gif", new[] { ".gif" }),

        // PSD: 8BPS
        new(0, new byte[] { 0x38, 0x42, 0x50, 0x53 }, "image/vnd.adobe.photoshop", new[] { ".psd" }),

        // RTF: {\rtf
        new(0, new byte[] { 0x7B, 0x5C, 0x72, 0x74, 0x66 }, "application/rtf", new[] { ".rtf" }),

        // FLAC: fLaC
        new(0, new byte[] { 0x66, 0x4C, 0x61, 0x43 }, "audio/flac", new[] { ".flac" }),

        // OGG: OggS
        new(0, new byte[] { 0x4F, 0x67, 0x67, 0x53 }, "audio/ogg", new[] { ".ogg", ".oga", ".ogv" }),

        // BMP: BM
        new(0, new byte[] { 0x42, 0x4D }, "image/bmp", new[] { ".bmp" }),

        // TIFF little-endian: II*\0
        new(0, new byte[] { 0x49, 0x49, 0x2A, 0x00 }, "image/tiff", new[] { ".tif", ".tiff" }),

        // TIFF big-endian: MM\0*
        new(0, new byte[] { 0x4D, 0x4D, 0x00, 0x2A }, "image/tiff", new[] { ".tif", ".tiff" }),

        // MP3 with ID3 tag
        new(0, new byte[] { 0x49, 0x44, 0x33 }, "audio/mpeg", new[] { ".mp3" }),

        // MP3 frame sync
        new(0, new byte[] { 0xFF, 0xFB }, "audio/mpeg", new[] { ".mp3" }),

        // ZIP-based (DOCX, XLSX, PPTX, ODT, ODS, ODP, EPUB, ZIP)
        new(0, new byte[] { 0x50, 0x4B, 0x03, 0x04 }, "application/zip",
            new[] { ".zip", ".docx", ".xlsx", ".pptx", ".odt", ".ods", ".odp", ".epub" }),
    };

    // RIFF-based signatures require checking bytes at offset 0 AND offset 8
    private static readonly List<RiffSignature> RiffSignatures = new()
    {
        // WebP: RIFF....WEBP
        new(new byte[] { 0x57, 0x45, 0x42, 0x50 }, "image/webp", new[] { ".webp" }),

        // WAV: RIFF....WAVE
        new(new byte[] { 0x57, 0x41, 0x56, 0x45 }, "audio/wav", new[] { ".wav" }),

        // AVI: RIFF....AVI
        new(new byte[] { 0x41, 0x56, 0x49, 0x20 }, "video/x-msvideo", new[] { ".avi" }),
    };

    // RIFF header: 52 49 46 46
    private static readonly byte[] RiffHeader = { 0x52, 0x49, 0x46, 0x46 };

    // MP4: ftyp at offset 4
    private static readonly byte[] Mp4Signature = { 0x66, 0x74, 0x79, 0x70 };

    /// <summary>
    /// Validates that the file's magic bytes match its declared extension.
    /// </summary>
    /// <param name="stream">The file stream to inspect. Position will be reset after reading.</param>
    /// <param name="fileName">The original file name with extension.</param>
    /// <param name="logger">Optional logger for warnings about mismatched types.</param>
    /// <returns>A tuple indicating whether the file is valid and the detected MIME type (null if unknown).</returns>
    public static (bool IsValid, string? DetectedMimeType) ValidateFileSignature(Stream stream, string fileName, ILogger? logger = null)
    {
        var extension = Path.GetExtension(fileName);

        // Skip validation for text-based formats (no reliable magic bytes)
        if (string.IsNullOrEmpty(extension) || TextBasedExtensions.Contains(extension))
        {
            return (true, null);
        }

        // Need at least 12 bytes to check all signatures (RIFF + subtype at offset 8)
        const int headerSize = 12;
        var header = new byte[headerSize];

        var originalPosition = stream.Position;
        try
        {
            stream.Position = 0;
            int bytesRead = stream.Read(header, 0, headerSize);

            if (bytesRead < 2)
            {
                // File too small to validate; allow it through
                return (true, null);
            }

            // Check MP4: "ftyp" at offset 4
            if (bytesRead >= 8 && MatchesAt(header, 4, Mp4Signature))
            {
                var mp4Mime = "video/mp4";
                var mp4Extensions = new[] { ".mp4", ".m4a", ".m4v", ".mov" };
                if (mp4Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    return (true, mp4Mime);
                }

                LogMismatch(logger, fileName, extension, mp4Mime);
                return (false, mp4Mime);
            }

            // Check RIFF-based formats (WebP, WAV, AVI)
            if (bytesRead >= 12 && MatchesAt(header, 0, RiffHeader))
            {
                foreach (var riff in RiffSignatures)
                {
                    if (MatchesAt(header, 8, riff.SubTypeBytes))
                    {
                        if (riff.ValidExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                        {
                            return (true, riff.MimeType);
                        }

                        LogMismatch(logger, fileName, extension, riff.MimeType);
                        return (false, riff.MimeType);
                    }
                }

                // Unknown RIFF subtype; allow through
                return (true, "application/octet-stream");
            }

            // Check standard magic byte signatures
            foreach (var sig in Signatures)
            {
                if (bytesRead >= sig.Offset + sig.MagicBytes.Length &&
                    MatchesAt(header, sig.Offset, sig.MagicBytes))
                {
                    if (sig.ValidExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                    {
                        return (true, sig.MimeType);
                    }

                    // Special case: ZIP signature matches many container formats
                    // If extension is a known ZIP-based format, allow it
                    if (sig.MimeType == "application/zip" &&
                        sig.ValidExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                    {
                        return (true, sig.MimeType);
                    }

                    LogMismatch(logger, fileName, extension, sig.MimeType);
                    return (false, sig.MimeType);
                }
            }

            // No signature matched -- could be a format without known magic bytes, allow through
            logger?.LogDebug("No magic byte signature matched for file {FileName} with extension {Extension}. Allowing through.",
                fileName, extension);
            return (true, null);
        }
        finally
        {
            // Always reset stream position
            stream.Position = originalPosition;
        }
    }

    private static bool MatchesAt(byte[] buffer, int offset, byte[] signature)
    {
        if (offset + signature.Length > buffer.Length) return false;
        for (int i = 0; i < signature.Length; i++)
        {
            if (buffer[offset + i] != signature[i]) return false;
        }
        return true;
    }

    private static void LogMismatch(ILogger? logger, string fileName, string extension, string detectedMime)
    {
        logger?.LogWarning(
            "File signature mismatch: {FileName} has extension {Extension} but magic bytes indicate {DetectedMimeType}. " +
            "The file may be misnamed or potentially malicious.",
            fileName, extension, detectedMime);
    }

    private sealed record MagicSignature(int Offset, byte[] MagicBytes, string MimeType, string[] ValidExtensions);
    private sealed record RiffSignature(byte[] SubTypeBytes, string MimeType, string[] ValidExtensions);
}
