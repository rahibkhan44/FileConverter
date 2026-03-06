# FileConverter -- Claude Instructions

## Project Identity
- **Goal**: World-class, free, open-source web-based file converter SaaS
- **Competitor benchmark**: FreeConvert.com (1,500+ formats, video/audio/image/document)
- **Stack**: .NET 9, ASP.NET Core, Blazor Server, Clean Architecture
- **Focus**: Web app first (FileConverter.Web + FileConverter.API). MAUI is paused.

## Architecture Rules
- **Domain -> Application -> Infrastructure -> API** (strict dependency direction)
- Domain has ZERO external package references
- Application references only Domain
- Infrastructure references Domain + Application
- API references all three
- Shared is a client library consumed by Web (and later MAUI)
- Web references only Shared (never API/Infrastructure directly)

## Library Policy -- CRITICAL
ALL libraries MUST be:
1. **Free and open-source** (MIT, Apache 2.0, LGPL, BSD, Unlicense, or similar)
2. **Self-contained NuGet packages** -- NO external tool downloads (NO LibreOffice, NO system-installed binaries)
3. **Cross-platform** (.NET 9 compatible)
4. **Commercially usable** -- no non-commercial-only licenses (no Openize.HEIC, no evaluation-only)

### Approved Libraries (verified free + self-contained)

**Images:**
- `Magick.NET-Q8-AnyCPU` (Apache 2.0) -- PRIMARY image engine. 200+ formats. Self-contained (bundles ImageMagick natively). Supports: PNG, JPG, WebP, GIF, BMP, TIFF, ICO, PSD, EPS, TGA, DDS, PCX, AVIF (read+write), HEIC (read only on most platforms, write blocked by patent). No external ImageMagick install needed.
- `SixLabors.ImageSharp` (Apache 2.0) -- secondary for lightweight raster ops, existing code compatibility
- `Svg.Skia` (MIT) -- high-quality SVG rendering to raster via SkiaSharp

**Documents:**
- `NPOI` (Apache 2.0) -- DOCX, XLSX, PPTX read/write. Legacy .doc/.xls/.ppt support exists but is limited for complex files. No LibreOffice needed.
- `DocumentFormat.OpenXml` (MIT) -- DOCX/PPTX/XLSX (Microsoft official)
- `RtfPipe` (MIT) -- RTF to HTML/text
- `Markdig` (BSD-2) -- Markdown parsing/rendering
- `HtmlAgilityPack` (MIT) -- HTML parsing and manipulation

**PDF:**
- `QuestPDF` (MIT Community, free < $1M revenue) -- PDF generation. If revenue exceeds $1M, paid license needed.
- `PdfPig` (Apache 2.0) -- PDF text/structure extraction
- `PdfSharpCore` (MIT) -- PDF merge, split, manipulate
- `PDFtoImage` (MIT) -- PDF page rasterization to PNG/JPG. Bundles PDFium+SkiaSharp. WARNING: NOT thread-safe, must use SemaphoreSlim(1) lock for concurrent calls.

**Spreadsheets:**
- `ClosedXML` (MIT) -- XLSX read/write (modern .xlsx only, NOT legacy .xls)
- `NPOI` (Apache 2.0) -- XLS/XLSX (NPOI handles legacy .xls better than ClosedXML)
- `CsvHelper` (MS-PL/Apache) -- CSV parsing/writing

**eBooks (later phase):**
- `VersOne.Epub` (Unlicense) -- EPUB reading only. Cannot create EPUB files.

**Video/Audio (Docker phase only):**
- `FFMpegCore` (MIT) -- .NET wrapper for FFmpeg
- FFmpeg binary bundled in Docker image (NOT a user download, NOT needed for non-Docker deployment)

### Banned / Do NOT Use
- ~~LibreOfficeConverter~~ -- requires system-installed binary. REMOVE completely.
- ~~Openize.HEIC~~ -- non-commercial license only, cannot use in SaaS
- ~~Aspose, Syncfusion, Telerik~~ -- paid/commercial
- ~~Xabe.FFmpeg~~ -- has license restrictions for commercial use
- ~~AngleSharp~~ -- not needed, HtmlAgilityPack covers HTML parsing
- ~~EpubSharp~~ -- abandoned/unmaintained
- ~~Docnet.Core~~ -- use PDFtoImage instead (same PDFium backend, better API)

### Known Limitations (document these in About page)
- **HEIC**: Can READ and convert HEIC to other formats via Magick.NET. Cannot WRITE/create HEIC files (patent restriction). This is the same as FreeConvert -- they convert FROM HEIC, not TO HEIC.
- **Legacy .doc/.xls/.ppt**: Basic read support via NPOI. Complex formatting (macros, OLE objects) may not convert perfectly. Clearly state "best effort" for legacy formats.
- **PDF to Image**: Single-threaded due to PDFium limitation. Use a dedicated SemaphoreSlim(1) for PDF rasterization to prevent crashes.
- **QuestPDF**: Free under $1M annual revenue. Upgrade license if needed.

## Conversion Engine Rules
1. Every route in `SupportedConversions.cs` MUST have a working native converter (no "only works if X is installed")
2. If a format conversion has known quality limitations (e.g., legacy .doc), still support it but log a warning
3. `ConversionEngineFactory` resolves converters by DI registration order -- register the best quality converter first
4. Every converter MUST handle all options it advertises (no phantom options like DPI that do nothing)
5. Every converter MUST report meaningful progress (not just fixed 10/30/50/100)
6. Long content MUST paginate properly in PDF output (no silent truncation)
7. Use `System.Threading.Channels` for job queue, not polling loops
8. PDF rasterization MUST use a SemaphoreSlim(1) lock (PDFium is not thread-safe)

## Code Standards
- Nullable enabled everywhere
- No `// TODO` without a linked GitHub issue
- No dead code -- if typed ConversionOptions exist, converters must use them
- No duplicate helper methods across converters -- extract shared logic
- Error messages must be user-friendly (not raw exception text)
- All new converters need unit tests
- Use `ILogger<T>` consistently (Serilog is the provider)

## File Size & Limits
- Max single file: 500MB (upgrade from 50MB)
- Max batch files: 100
- Max batch total: 2GB
- Rate limit: configurable via appsettings.json (default 50/day free tier)
- Job expiry: 2 hours (upgrade from 1 hour)

## API Conventions
- POST returns 202 Accepted (not 200) for async jobs
- Download endpoints return original filename with new extension
- All responses include `statusUrl` and `downloadUrl` hypermedia links
- API versioned via URL path (`/api/v1/`, `/api/v2/`)
- Correlation ID header on all requests/responses

## Web App (Blazor) Focus
- Interactive Server rendering
- Responsive design (mobile-first)
- Drag-and-drop file upload with preview
- Real-time progress via SignalR (replace polling)
- Per-format dedicated pages for SEO (e.g., `/convert/heic-to-jpg`)
- Dark mode support
- Accessible (WCAG 2.1 AA)

## Testing
- xUnit + no mocking framework (use real implementations)
- Test every converter with real sample files in `tests/TestFiles/`
- Test invalid inputs, corrupt files, oversized files
- Integration tests for API endpoints

## Docker
- Dockerfile for API + Web
- docker-compose.yml for full stack
- FFmpeg pre-installed in image (for video/audio phases)
- Health check endpoints: `/health/live`, `/health/ready`

## Git
- Main branch: `main`
- Feature branches: `feature/<name>`
- Conventional commits: `feat:`, `fix:`, `refactor:`, `test:`, `docs:`
