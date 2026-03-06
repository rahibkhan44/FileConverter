using FileConverter.Application;
using FileConverter.Domain.Enums;

namespace FileConverter.API.Tests;

public class SupportedConversionsTests
{
    [Theory]
    [InlineData(FileFormat.Png, FileFormat.Jpg, true)]
    [InlineData(FileFormat.Png, FileFormat.WebP, true)]
    [InlineData(FileFormat.Docx, FileFormat.Pdf, true)]
    [InlineData(FileFormat.Pdf, FileFormat.Txt, true)]
    [InlineData(FileFormat.Csv, FileFormat.Xlsx, true)]
    [InlineData(FileFormat.Svg, FileFormat.Png, true)]
    [InlineData(FileFormat.Png, FileFormat.Docx, false)]
    [InlineData(FileFormat.Pdf, FileFormat.Docx, false)]
    [InlineData(FileFormat.Svg, FileFormat.Pdf, true)]
    public void IsSupported_ReturnsCorrectResult(FileFormat source, FileFormat target, bool expected)
    {
        Assert.Equal(expected, SupportedConversions.IsSupported(source, target));
    }

    [Theory]
    [InlineData(".png", FileFormat.Png)]
    [InlineData(".jpg", FileFormat.Jpg)]
    [InlineData(".jpeg", FileFormat.Jpg)]
    [InlineData(".pdf", FileFormat.Pdf)]
    [InlineData(".docx", FileFormat.Docx)]
    [InlineData(".CSV", FileFormat.Csv)]
    public void ParseFormat_ReturnsCorrectFormat(string ext, FileFormat expected)
    {
        Assert.Equal(expected, SupportedConversions.ParseFormat(ext));
    }

    [Fact]
    public void ParseFormat_UnknownExtension_ReturnsNull()
    {
        Assert.Null(SupportedConversions.ParseFormat(".xyz"));
    }

    [Fact]
    public void GetTargets_Png_HasExpectedTargets()
    {
        var targets = SupportedConversions.GetTargets(FileFormat.Png);
        Assert.Contains(FileFormat.Jpg, targets);
        Assert.Contains(FileFormat.WebP, targets);
        Assert.Contains(FileFormat.Pdf, targets);
        Assert.DoesNotContain(FileFormat.Docx, targets);
    }

    [Fact]
    public void GetExtension_ReturnsCorrect()
    {
        Assert.Equal(".pdf", SupportedConversions.GetExtension(FileFormat.Pdf));
        Assert.Equal(".jpg", SupportedConversions.GetExtension(FileFormat.Jpg));
    }
}
