using FileConverter.Domain.Enums;
using FileConverter.Domain.Interfaces;

namespace FileConverter.Infrastructure.Converters;

public class ConversionEngineFactory
{
    private readonly IEnumerable<IFileConverter> _converters;

    public ConversionEngineFactory(IEnumerable<IFileConverter> converters)
    {
        _converters = converters;
    }

    public IFileConverter GetConverter(FileFormat source, FileFormat target)
    {
        return _converters.FirstOrDefault(c => c.CanConvert(source, target))
            ?? throw new NotSupportedException($"No converter available for {source} → {target}");
    }
}
