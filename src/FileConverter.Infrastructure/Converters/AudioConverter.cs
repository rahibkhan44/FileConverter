using FileConverter.Application;
using FileConverter.Domain.Enums;
using FileConverter.Domain.Interfaces;
using FFMpegCore;
using FFMpegCore.Enums;
using Microsoft.Extensions.Logging;

namespace FileConverter.Infrastructure.Converters;

/// <summary>
/// Audio converter using FFMpegCore. Requires FFmpeg binary (bundled in Docker image).
/// Handles audio-to-audio conversion with bitrate, sample rate, and trim options.
/// </summary>
public class AudioConverter : IFileConverter
{
    private readonly ILogger<AudioConverter> _logger;

    private static readonly HashSet<FileFormat> AudioFormats = new()
    {
        FileFormat.Mp3, FileFormat.Wav, FileFormat.Flac, FileFormat.Aac,
        FileFormat.Ogg, FileFormat.Wma, FileFormat.M4a, FileFormat.Opus
    };

    public AudioConverter(ILogger<AudioConverter> logger)
    {
        _logger = logger;
    }

    public bool CanConvert(FileFormat source, FileFormat target)
        => AudioFormats.Contains(source) && AudioFormats.Contains(target) && source != target;

    public async Task<string> ConvertAsync(string inputPath, string outputDirectory, FileFormat targetFormat,
        Dictionary<string, string> options, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(5);

        var extension = SupportedConversions.GetExtension(targetFormat);
        var outputPath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(inputPath) + extension);

        progress?.Report(10);

        var processor = FFMpegArguments
            .FromFileInput(inputPath)
            .OutputToFile(outputPath, overwrite: true, opts =>
            {
                // Audio codec
                var codec = GetAudioCodec(targetFormat);
                if (codec != null) opts.WithAudioCodec(codec);

                // Bitrate
                if (options.TryGetValue("bitrate", out var brStr) && int.TryParse(brStr, out var bitrate))
                    opts.WithAudioBitrate(bitrate);

                // Sample rate
                if (options.TryGetValue("sampleRate", out var srStr) && int.TryParse(srStr, out var sampleRate))
                    opts.WithAudioSamplingRate(sampleRate);

                // Channels
                if (options.TryGetValue("channels", out var chStr) && chStr == "1")
                    opts.WithCustomArgument("-ac 1");
                else if (options.TryGetValue("channels", out var ch2) && ch2 == "2")
                    opts.WithCustomArgument("-ac 2");

                // Trim
                if (options.TryGetValue("trimStart", out var startStr) && double.TryParse(startStr, out var start))
                    opts.WithCustomArgument($"-ss {start}");
                if (options.TryGetValue("trimEnd", out var endStr) && double.TryParse(endStr, out var end))
                    opts.WithCustomArgument($"-to {end}");

                // No video stream
                opts.WithCustomArgument("-vn");
            });

        progress?.Report(20);
        await processor.ProcessAsynchronously(true);
        progress?.Report(100);

        _logger.LogInformation("Audio conversion completed: {Input} -> {Output}", inputPath, outputPath);
        return outputPath;
    }

    private static string? GetAudioCodec(FileFormat target)
    {
        return target switch
        {
            FileFormat.Mp3 => "libmp3lame",
            FileFormat.Aac or FileFormat.M4a => "aac",
            FileFormat.Flac => "flac",
            FileFormat.Wav => "pcm_s16le",
            FileFormat.Ogg => "libvorbis",
            FileFormat.Opus => "libopus",
            FileFormat.Wma => "wmav2",
            _ => null
        };
    }
}
