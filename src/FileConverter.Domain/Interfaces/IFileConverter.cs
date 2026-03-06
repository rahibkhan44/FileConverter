using FileConverter.Domain.Enums;

namespace FileConverter.Domain.Interfaces;

public interface IFileConverter
{
    bool CanConvert(FileFormat source, FileFormat target);
    Task<string> ConvertAsync(string inputPath, string outputDirectory, FileFormat targetFormat,
        Dictionary<string, string> options, IProgress<int>? progress = null, CancellationToken cancellationToken = default);
}
