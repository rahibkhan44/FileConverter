using FileConverter.Application;
using FileConverter.Domain.Enums;
using FileConverter.Domain.Interfaces;
using FFMpegCore;
using FFMpegCore.Enums;
using Microsoft.Extensions.Logging;

namespace FileConverter.Infrastructure.Converters;

/// <summary>
/// Video converter using FFMpegCore. Requires FFmpeg binary (bundled in Docker image).
/// Handles video-to-video conversion, video-to-GIF, and video-to-audio extraction.
/// </summary>
public class VideoConverter : IFileConverter
{
    private readonly ILogger<VideoConverter> _logger;

    private static readonly HashSet<FileFormat> VideoInputs = new()
    {
        FileFormat.Mp4, FileFormat.Mkv, FileFormat.WebM, FileFormat.Avi,
        FileFormat.Mov, FileFormat.Flv, FileFormat.Wmv, FileFormat.Ts
    };

    private static readonly HashSet<FileFormat> VideoTargets = new()
    {
        FileFormat.Mp4, FileFormat.Mkv, FileFormat.WebM, FileFormat.Avi,
        FileFormat.Mov, FileFormat.Flv, FileFormat.Wmv, FileFormat.Ts,
        FileFormat.Gif
    };

    private static readonly HashSet<FileFormat> AudioExtractTargets = new()
    {
        FileFormat.Mp3, FileFormat.Wav, FileFormat.Aac
    };

    public VideoConverter(ILogger<VideoConverter> logger)
    {
        _logger = logger;
    }

    public bool CanConvert(FileFormat source, FileFormat target)
        => VideoInputs.Contains(source) && (VideoTargets.Contains(target) || AudioExtractTargets.Contains(target)) && source != target;

    public async Task<string> ConvertAsync(string inputPath, string outputDirectory, FileFormat targetFormat,
        Dictionary<string, string> options, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(5);

        var extension = SupportedConversions.GetExtension(targetFormat);
        var outputPath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(inputPath) + extension);

        var mediaInfo = await FFProbe.AnalyseAsync(inputPath, cancellationToken: cancellationToken);
        progress?.Report(10);

        if (AudioExtractTargets.Contains(targetFormat))
        {
            await ExtractAudio(inputPath, outputPath, targetFormat, options, mediaInfo, progress, cancellationToken);
        }
        else if (targetFormat == FileFormat.Gif)
        {
            await ConvertToGif(inputPath, outputPath, options, mediaInfo, progress, cancellationToken);
        }
        else
        {
            await ConvertVideo(inputPath, outputPath, targetFormat, options, mediaInfo, progress, cancellationToken);
        }

        progress?.Report(100);
        _logger.LogInformation("Video conversion completed: {Input} -> {Output}", inputPath, outputPath);
        return outputPath;
    }

    private async Task ConvertVideo(string inputPath, string outputPath, FileFormat targetFormat,
        Dictionary<string, string> options, IMediaAnalysis mediaInfo, IProgress<int>? progress, CancellationToken ct)
    {
        var processor = FFMpegArguments
            .FromFileInput(inputPath)
            .OutputToFile(outputPath, overwrite: true, opts =>
            {
                // Video codec
                var codec = GetVideoCodec(targetFormat, options);
                if (codec != null) opts.WithVideoCodec(codec);

                // Resolution
                if (options.TryGetValue("resolution", out var res) && !string.IsNullOrEmpty(res))
                {
                    var parts = res.Split('x');
                    if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
                        opts.WithCustomArgument($"-vf scale={w}:{h}");
                }

                // Video bitrate
                if (options.TryGetValue("videoBitrate", out var vbr) && int.TryParse(vbr, out var videoBitrate))
                    opts.WithVideoBitrate(videoBitrate);

                // FPS
                if (options.TryGetValue("fps", out var fpsStr) && double.TryParse(fpsStr, out var fps))
                    opts.WithFramerate(fps);

                // Audio codec
                var audioCodec = GetAudioCodecForContainer(targetFormat);
                if (audioCodec != null) opts.WithAudioCodec(audioCodec);

                // Audio bitrate
                if (options.TryGetValue("audioBitrate", out var abr) && int.TryParse(abr, out var audioBitrate))
                    opts.WithAudioBitrate(audioBitrate);

                // Trim
                ApplyTrim(opts, options);
            });

        progress?.Report(20);
        await processor.ProcessAsynchronously(true);
        progress?.Report(90);
    }

    private async Task ConvertToGif(string inputPath, string outputPath,
        Dictionary<string, string> options, IMediaAnalysis mediaInfo, IProgress<int>? progress, CancellationToken ct)
    {
        var fps = 10;
        if (options.TryGetValue("fps", out var fpsStr) && int.TryParse(fpsStr, out var parsedFps))
            fps = Math.Clamp(parsedFps, 1, 30);

        var width = 480;
        if (options.TryGetValue("width", out var wStr) && int.TryParse(wStr, out var parsedWidth))
            width = Math.Clamp(parsedWidth, 100, 1920);

        var maxDuration = 30;
        if (options.TryGetValue("maxDuration", out var durStr) && int.TryParse(durStr, out var parsedDur))
            maxDuration = Math.Clamp(parsedDur, 1, 60);

        var processor = FFMpegArguments
            .FromFileInput(inputPath, verifyExists: true, opts =>
            {
                if (mediaInfo.Duration.TotalSeconds > maxDuration)
                    opts.WithDuration(TimeSpan.FromSeconds(maxDuration));
                ApplyTrimInput(opts, options);
            })
            .OutputToFile(outputPath, overwrite: true, opts =>
            {
                opts.WithCustomArgument($"-vf \"fps={fps},scale={width}:-1:flags=lanczos\"");
                opts.WithCustomArgument("-loop 0");
            });

        progress?.Report(20);
        await processor.ProcessAsynchronously(true);
        progress?.Report(90);
    }

    private async Task ExtractAudio(string inputPath, string outputPath, FileFormat targetFormat,
        Dictionary<string, string> options, IMediaAnalysis mediaInfo, IProgress<int>? progress, CancellationToken ct)
    {
        var processor = FFMpegArguments
            .FromFileInput(inputPath)
            .OutputToFile(outputPath, overwrite: true, opts =>
            {
                opts.WithCustomArgument("-vn"); // no video

                var codec = targetFormat switch
                {
                    FileFormat.Mp3 => "libmp3lame",
                    FileFormat.Aac => "aac",
                    _ => (string?)null
                };
                if (codec != null) opts.WithAudioCodec(codec);

                if (options.TryGetValue("audioBitrate", out var abr) && int.TryParse(abr, out var bitrate))
                    opts.WithAudioBitrate(bitrate);

                ApplyTrim(opts, options);
            });

        progress?.Report(20);
        await processor.ProcessAsynchronously(true);
        progress?.Report(90);
    }

    private static string? GetVideoCodec(FileFormat target, Dictionary<string, string> options)
    {
        if (options.TryGetValue("videoCodec", out var custom) && !string.IsNullOrEmpty(custom))
            return custom;

        return target switch
        {
            FileFormat.Mp4 => "libx264",
            FileFormat.WebM => "libvpx-vp9",
            FileFormat.Mkv => "libx264",
            FileFormat.Avi => "mpeg4",
            FileFormat.Mov => "libx264",
            FileFormat.Flv => "flv",
            FileFormat.Wmv => "wmv2",
            FileFormat.Ts => "libx264",
            _ => null
        };
    }

    private static string? GetAudioCodecForContainer(FileFormat target)
    {
        return target switch
        {
            FileFormat.Mp4 or FileFormat.Mov or FileFormat.Ts => "aac",
            FileFormat.WebM => "libopus",
            FileFormat.Mkv => "aac",
            FileFormat.Avi => "mp3",
            FileFormat.Flv => "aac",
            FileFormat.Wmv => "wmav2",
            _ => null
        };
    }

    private static void ApplyTrim(FFMpegArgumentOptions opts, Dictionary<string, string> options)
    {
        if (options.TryGetValue("trimStart", out var startStr) && double.TryParse(startStr, out var start))
            opts.WithCustomArgument($"-ss {start}");
        if (options.TryGetValue("trimEnd", out var endStr) && double.TryParse(endStr, out var end))
            opts.WithCustomArgument($"-to {end}");
    }

    private static void ApplyTrimInput(FFMpegArgumentOptions opts, Dictionary<string, string> options)
    {
        if (options.TryGetValue("trimStart", out var startStr) && double.TryParse(startStr, out var start))
            opts.Seek(TimeSpan.FromSeconds(start));
    }
}
