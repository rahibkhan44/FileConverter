using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using UglyToad.PdfPig;
using PdfDocument = PdfSharpCore.Pdf.PdfDocument;

namespace FileConverter.Infrastructure.Services;

/// <summary>
/// PDF power tools: merge, split, compress, page count.
/// Uses PdfSharpCore for manipulation and PdfPig for reading.
/// </summary>
public class PdfToolsService
{
    /// <summary>
    /// Merges multiple PDF files into a single output PDF.
    /// </summary>
    public string Merge(IEnumerable<string> inputPaths, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var outputDoc = new PdfDocument();

        foreach (var inputPath in inputPaths)
        {
            using var inputDoc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
            for (int i = 0; i < inputDoc.PageCount; i++)
            {
                outputDoc.AddPage(inputDoc.Pages[i]);
            }
        }

        outputDoc.Save(outputPath);
        return outputPath;
    }

    /// <summary>
    /// Splits a PDF into separate files based on page ranges.
    /// Returns list of output file paths.
    /// </summary>
    public List<string> Split(string inputPath, string outputDirectory, IEnumerable<(int Start, int End)> pageRanges)
    {
        Directory.CreateDirectory(outputDirectory);
        var results = new List<string>();
        var baseName = Path.GetFileNameWithoutExtension(inputPath);

        using var inputDoc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
        int rangeIndex = 1;

        foreach (var (start, end) in pageRanges)
        {
            var outputDoc = new PdfDocument();
            int actualStart = Math.Max(1, start) - 1; // Convert to 0-based
            int actualEnd = Math.Min(end, inputDoc.PageCount) - 1;

            for (int i = actualStart; i <= actualEnd; i++)
            {
                outputDoc.AddPage(inputDoc.Pages[i]);
            }

            var outputPath = Path.Combine(outputDirectory, $"{baseName}_pages_{start}-{end}.pdf");
            outputDoc.Save(outputPath);
            outputDoc.Dispose();
            results.Add(outputPath);
            rangeIndex++;
        }

        return results;
    }

    /// <summary>
    /// Splits a PDF into individual pages. Returns list of output file paths.
    /// </summary>
    public List<string> SplitAllPages(string inputPath, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var results = new List<string>();
        var baseName = Path.GetFileNameWithoutExtension(inputPath);

        using var inputDoc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);

        for (int i = 0; i < inputDoc.PageCount; i++)
        {
            var outputDoc = new PdfDocument();
            outputDoc.AddPage(inputDoc.Pages[i]);

            var outputPath = Path.Combine(outputDirectory, $"{baseName}_page_{i + 1}.pdf");
            outputDoc.Save(outputPath);
            outputDoc.Dispose();
            results.Add(outputPath);
        }

        return results;
    }

    /// <summary>
    /// Gets the page count of a PDF.
    /// </summary>
    public int GetPageCount(string inputPath)
    {
        using var doc = UglyToad.PdfPig.PdfDocument.Open(inputPath);
        return doc.NumberOfPages;
    }

    /// <summary>
    /// Extracts specific pages from a PDF into a new PDF.
    /// </summary>
    public string ExtractPages(string inputPath, string outputPath, IEnumerable<int> pageNumbers)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var inputDoc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
        using var outputDoc = new PdfDocument();

        foreach (var pageNum in pageNumbers)
        {
            int index = pageNum - 1;
            if (index >= 0 && index < inputDoc.PageCount)
                outputDoc.AddPage(inputDoc.Pages[index]);
        }

        outputDoc.Save(outputPath);
        return outputPath;
    }

    /// <summary>
    /// Rotates all pages of a PDF by the specified degrees (90, 180, 270).
    /// </summary>
    public string RotatePages(string inputPath, string outputPath, int degrees)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var inputDoc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Modify);

        foreach (var page in inputDoc.Pages)
        {
            page.Rotate = (page.Rotate + degrees) % 360;
        }

        inputDoc.Save(outputPath);
        return outputPath;
    }

    /// <summary>
    /// Adds a text watermark to every page of a PDF.
    /// Uses PdfSharpCore drawing.
    /// </summary>
    public string AddWatermark(string inputPath, string outputPath, string text, double fontSize = 48, double opacity = 0.3)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var doc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Modify);

        var color = PdfSharpCore.Drawing.XColor.FromArgb((int)(opacity * 255), 128, 128, 128);
        var font = new PdfSharpCore.Drawing.XFont("Arial", fontSize);
        var brush = new PdfSharpCore.Drawing.XSolidBrush(color);

        foreach (var page in doc.Pages)
        {
            using var gfx = PdfSharpCore.Drawing.XGraphics.FromPdfPage(page, PdfSharpCore.Drawing.XGraphicsPdfPageOptions.Append);
            var size = gfx.MeasureString(text, font);

            // Center and rotate 45 degrees
            gfx.TranslateTransform(page.Width.Point / 2, page.Height.Point / 2);
            gfx.RotateTransform(-45);
            gfx.DrawString(text, font, brush,
                new PdfSharpCore.Drawing.XPoint(-size.Width / 2, size.Height / 2));
        }

        doc.Save(outputPath);
        return outputPath;
    }

    /// <summary>
    /// Reads PDF metadata (title, author, subject, keywords, creator, producer, creation/modification dates).
    /// </summary>
    public PdfMetadata GetMetadata(string inputPath)
    {
        using var doc = UglyToad.PdfPig.PdfDocument.Open(inputPath);
        var info = doc.Information;

        return new PdfMetadata
        {
            Title = info.Title ?? "",
            Author = info.Author ?? "",
            Subject = info.Subject ?? "",
            Keywords = info.Keywords ?? "",
            Creator = info.Creator ?? "",
            Producer = info.Producer ?? "",
            PageCount = doc.NumberOfPages
        };
    }

    public class PdfMetadata
    {
        public string Title { get; set; } = "";
        public string Author { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Keywords { get; set; } = "";
        public string Creator { get; set; } = "";
        public string Producer { get; set; } = "";
        public int PageCount { get; set; }
    }
}
