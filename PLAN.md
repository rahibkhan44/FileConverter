# FileConverter — Master Implementation Plan

**Goal**: Build the world's top free web-based file converter, rivaling FreeConvert.com
**Strategy**: Pure .NET NuGet packages only — no LibreOffice, no external installs
**Focus**: Web app (Blazor Server + API). MAUI paused.

---

## Current State (22 formats, ~80 conversion paths)

| Category | Formats | Engine |
|---|---|---|
| Images | PNG, JPG, WebP, GIF, BMP, TIFF, SVG, ICO | ImageSharp, Svg.Skia |
| Documents | DOCX, DOC, ODT, RTF, TXT, HTML | LibreOffice (external!) + OpenXml + RtfPipe |
| PDF | PDF | QuestPDF + PdfPig |
| Spreadsheets | XLSX, XLS, ODS, CSV | ClosedXML + LibreOffice |
| Presentations | PPTX, PPT, ODP | LibreOffice only |

**Critical problem**: ~60% of document/spreadsheet/presentation conversions ONLY work if LibreOffice is installed on the server. No LibreOffice = broken product.

---

## Target State (50+ formats, 500+ conversion paths)

| Category | Formats | Engine |
|---|---|---|
| Images | PNG, JPG, WebP, GIF, BMP, TIFF, SVG, ICO, HEIC (read), AVIF, PSD, EPS, TGA, RAW (CR2/NEF/ARW), JFIF, JP2, APNG | Magick.NET (self-contained) |
| Documents | DOCX, DOC (basic), ODT, RTF, TXT, HTML, MD (Markdown), EPUB (read) | NPOI + OpenXml + Markdig + HtmlAgilityPack + VersOne.Epub |
| PDF | PDF (read/write/merge/split/compress/rasterize) | QuestPDF + PdfPig + PdfSharpCore + PDFtoImage (thread-locked) |
| Spreadsheets | XLSX, XLS (basic), ODS, CSV, TSV | NPOI + ClosedXML + CsvHelper |
| Presentations | PPTX, PPT, ODP | NPOI + OpenXml |
| Video (Phase 4) | MP4, MKV, WebM, AVI, MOV, FLV, WMV, GIF | FFMpegCore (Docker-bundled) |
| Audio (Phase 4) | MP3, WAV, FLAC, AAC, OGG, WMA, M4A | FFMpegCore (Docker-bundled) |

### Verified Library Limitations (be honest with users)

| Library | Limitation | Mitigation |
|---|---|---|
| **Magick.NET** | HEIC: read-only (patent blocks write). On Windows may need env setup for HEIC codecs. | Convert FROM HEIC to JPG/PNG/WebP. Same as FreeConvert. |
| **NPOI** | Legacy .doc/.xls/.ppt: basic support, complex formatting may degrade | Show "best effort" warning for legacy formats |
| **QuestPDF** | Free only under $1M annual revenue | Fine for now, upgrade license when successful |
| **PDFtoImage** | PDFium is NOT thread-safe | Use `SemaphoreSlim(1)` lock for all PDF rasterization |
| **ClosedXML** | Cannot read legacy .xls (only .xlsx) | Use NPOI for .xls, ClosedXML for .xlsx |
| **VersOne.Epub** | Read-only (cannot create EPUB files) | Support EPUB as input only (convert EPUB to PDF/TXT/HTML) |
| **Openize.HEIC** | Non-commercial license -- BANNED | Use Magick.NET for HEIC instead |

---

## Phase 1: Fix Bugs + Replace LibreOffice (Week 1-2)

**Goal**: Every advertised conversion works with zero external dependencies.

### 1.1 — Remove LibreOffice Dependency

| File | Action |
|---|---|
| `Infrastructure/Converters/LibreOfficeConverter.cs` | DELETE entirely |
| `API/Program.cs` | Remove LibreOffice DI registration and detection log |
| `Infrastructure/Infrastructure.csproj` | Add `NPOI` (2.7.2+) package |

### 1.2 — Replace LibreOffice Routes with Native Converters

**New file: `Infrastructure/Converters/NpoiDocumentConverter.cs`**
Handles DOC/DOCX/ODT/RTF/TXT/HTML interconversion using NPOI:
- DOC → DOCX, TXT, PDF, HTML, RTF, ODT
- DOCX → DOC, TXT, PDF, HTML, RTF, ODT
- TXT → DOCX, DOC, PDF, HTML, RTF
- HTML → DOCX, DOC, PDF, TXT, RTF
- RTF → DOCX, DOC, PDF, TXT, HTML (replace current RtfConverter)
- ODT → DOCX, DOC, PDF, TXT, HTML, RTF

**New file: `Infrastructure/Converters/NpoiSpreadsheetConverter.cs`**
Handles XLS/XLSX/ODS/CSV using NPOI + ClosedXML:
- XLS → XLSX, CSV, PDF, ODS, HTML, TXT
- XLSX → XLS, CSV, PDF, ODS, HTML, TXT
- CSV → XLSX, XLS, PDF, ODS, HTML
- ODS → XLSX, XLS, CSV, PDF, HTML, TXT

**New file: `Infrastructure/Converters/NpoiPresentationConverter.cs`**
Handles PPT/PPTX/ODP using NPOI:
- PPT → PPTX, PDF, TXT, HTML, ODP
- PPTX → PPT, PDF, TXT, HTML, ODP
- ODP → PPTX, PPT, PDF, TXT, HTML

### 1.3 — Fix Existing Bugs

| Bug | File | Fix |
|---|---|---|
| DPI option ignored | `ImageConverter.cs` | Read `dpi` from options, apply to ImageSharp metadata |
| RtfConverter hardcodes A4 | `RtfConverter.cs` | Read `pageSize` and `orientation` from options (match PdfConverter) |
| SpreadsheetConverter includeHeaders dead | `SpreadsheetConverter.cs` | Honor `includeHeaders` in XLSX output path |
| BatchConversionJob all-failed = Completed | `BatchConversionJob.cs` | Add `Failed` state when ALL jobs failed |
| POST /convert returns 200 | `ConvertController.cs` | Return `202 Accepted` with Location header |
| File size checked after full upload | `ConversionService.cs` | Check `Content-Length` header before reading stream; stream with limit |
| ConversionWorker picks up all jobs at once | `ConversionWorker.cs` | Replace polling with `Channel<Guid>` bounded queue |
| QuestPDF long content truncated | `PdfConverter.cs`, `DocumentConverter.cs` | Use `Column()` container with `Text()` inside for auto-pagination |
| Dead ConversionOptions classes | All converters | Use typed `ImageConversionOptions`/`PdfConversionOptions`/`SpreadsheetConversionOptions` instead of raw dictionary |
| Download returns UUID filename | `ConvertController.cs` | Use `{originalName}.{newExtension}` in Content-Disposition |
| Duplicate GeneratePdfFromText | `PdfConverter.cs`, `DocumentConverter.cs`, `RtfConverter.cs` | Extract to shared `PdfGenerationHelper.cs` |

### 1.4 — Infrastructure Improvements

| File | Action |
|---|---|
| `API/Program.cs` | Add health checks (`/health/live`, `/health/ready`) |
| `ConvertController.cs` | Add `statusUrl` and `downloadUrl` in all responses |
| `ConversionService.cs` | Raise file limit to 500MB, batch to 2GB |
| `appsettings.json` | Make rate limit, file size, expiry configurable |
| `ConvertController.cs` | Add job cancellation endpoint `DELETE /api/v1/convert/{id}` |
| `ConversionJob.cs` | Add `RetryCount`, `MaxRetries`, `IsCancelled` properties |

---

## Phase 2: Massive Format Expansion with Magick.NET (Week 2-3)

**Goal**: Go from 8 image formats to 50+ and add PDF tools.

### 2.1 — Replace ImageSharp with Magick.NET as Primary Image Engine

| File | Action |
|---|---|
| `Infrastructure.csproj` | Add `Magick.NET-Q8-AnyCPU` (latest), add `PDFtoImage` |
| **New: `MagickImageConverter.cs`** | Primary image converter using Magick.NET |

**MagickImageConverter** handles ALL image-to-image conversions:
- Input: PNG, JPG, WebP, GIF, BMP, TIFF, SVG, ICO, HEIC (read-only, patent), AVIF, PSD, EPS, TGA, JP2, JFIF, APNG, DDS, PCX, WBMP, PBM, PGM, PPM, XBM, XPM, and 100+ RAW camera formats (CR2, NEF, ARW, DNG, ORF, RAF, RW2, etc.)
- Output: PNG, JPG, WebP, GIF, BMP, TIFF, SVG, ICO, PDF (HEIC output NOT supported -- patent)
- Options: quality, width, height, DPI, maintainAspectRatio, strip metadata, color profile, background color
- NOTE: HEIC appears only as INPUT source in SupportedConversions, never as a target

**Register before old ImageConverter in DI** so Magick.NET wins all routes.

### 2.2 — Add New Image Formats to Domain

| File | Action |
|---|---|
| `Enums/FileFormat.cs` | Add: Heic, Avif, Psd, Eps, Tga, Jp2, Jfif, Apng, Dng, Cr2, Nef, Arw |
| `SupportedConversions.cs` | Add all new format entries with their target sets |
| `SupportedConversions.cs` | Update `ParseFormat`, `GetExtension`, `GetCategory` |
| `Shared/ClientSupportedFormats.cs` | Mirror all new formats |
| `FormatOptionsProvider.cs` | Add options for new formats |

### 2.3 — PDF Power Tools

**New file: `Infrastructure/Converters/PdfToolsService.cs`**
Using PdfSharpCore + Docnet.Core:
- **PDF merge**: Combine multiple PDFs into one
- **PDF split**: Split PDF by page ranges
- **PDF compress**: Reduce PDF file size
- **PDF to images**: Rasterize each page to PNG/JPG (PDFtoImage + SemaphoreSlim(1) lock -- NOT thread-safe)
- **PDF page rotation**: Rotate specific pages
- **PDF metadata**: Read/write title, author, etc.

**New file: `API/Controllers/PdfToolsController.cs`**
- `POST /api/v1/pdf/merge` — upload multiple PDFs, get one
- `POST /api/v1/pdf/split` — upload PDF + page ranges, get ZIP
- `POST /api/v1/pdf/compress` — upload PDF, get compressed
- `POST /api/v1/pdf/to-images` — upload PDF, get ZIP of images

### 2.4 — Image Tools

**New file: `API/Controllers/ImageToolsController.cs`**
Using Magick.NET:
- `POST /api/v1/image/compress` — reduce file size with quality setting
- `POST /api/v1/image/resize` — resize with dimensions
- `POST /api/v1/image/crop` — crop with coordinates
- `POST /api/v1/image/rotate` — rotate by degrees
- `POST /api/v1/image/watermark` — add text/image watermark
- `POST /api/v1/image/metadata` — strip or read EXIF data

### 2.5 — Add Markdown Support

| File | Action |
|---|---|
| `Infrastructure.csproj` | Add `Markdig` package |
| `Enums/FileFormat.cs` | Add `Md` format |
| `SupportedConversions.cs` | Md → HTML, PDF, DOCX, TXT; HTML/TXT → Md |
| **New: `MarkdownConverter.cs`** | Markdig for MD↔HTML, MD→PDF (via HTML→QuestPDF), MD→TXT |

---

## Phase 3: Web App — World-Class UI (Week 3-5)

**Goal**: Build a polished, SEO-optimized Blazor web app that rivals FreeConvert.com.

### 3.1 — SEO-Optimized Conversion Pages

**New: `Components/Pages/ConvertFormat.razor`**
- Route: `/convert/{source}-to-{target}` (e.g., `/convert/heic-to-jpg`)
- Auto-generated for every supported conversion pair
- Each page has: title, description, FAQ, drag-drop converter
- Server-side rendered for SEO (SSR first, then interactive)

**New: `Components/Pages/FormatPage.razor`**
- Route: `/formats/{format}` (e.g., `/formats/heic`)
- Lists all conversions available for that format
- Format description, technical details

**New: `Components/Pages/AllFormats.razor`**
- Route: `/formats`
- Grid of all supported formats grouped by category
- Search/filter functionality

### 3.2 — Enhanced Home Page

| File | Action |
|---|---|
| `Components/Pages/Home.razor` | Complete redesign |

Features:
- Hero section with animated file drop zone
- Format auto-detection on upload
- Smart target format suggestion based on source
- Drag-and-drop with file preview (thumbnails for images, icon for docs)
- Batch upload progress with individual file cards
- One-click "Convert All" + "Download All as ZIP"
- Recent conversions (localStorage)
- Format search bar ("What do you want to convert?")

### 3.3 — Real-Time Progress via SignalR

| File | Action |
|---|---|
| **New: `API/Hubs/ConversionHub.cs`** | SignalR hub for real-time job updates |
| `ConversionWorker.cs` | Push progress updates via hub |
| `Web/Components/Pages/Home.razor` | Connect to SignalR instead of polling |
| `API/Program.cs` | `app.MapHub<ConversionHub>("/hubs/conversion")` |

### 3.4 — Dark Mode + Responsive Design

| File | Action |
|---|---|
| `Web/wwwroot/css/` | Tailwind CSS or Bootstrap 5.3 with CSS variables |
| `Components/Layout/MainLayout.razor` | Theme toggle (dark/light/system) |
| All components | Mobile-first responsive breakpoints |

### 3.5 — PDF & Image Tool Pages

**New: `Components/Pages/PdfTools.razor`**
- Route: `/tools/pdf`
- Sub-pages: merge, split, compress, rotate, to-images
- Drag-drop PDF upload, visual page selector for split

**New: `Components/Pages/ImageTools.razor`**
- Route: `/tools/images`
- Sub-pages: compress, resize, crop, rotate, watermark
- Visual preview with before/after comparison

### 3.6 — Accessibility & Performance

- WCAG 2.1 AA compliance (aria labels, keyboard nav, contrast)
- Lazy loading for images and components
- Response compression middleware
- Static asset caching headers
- Bundle size optimization

---

## Phase 4: Video & Audio (Week 5-7)

**Goal**: Add the most-requested conversion category — video and audio.

### 4.1 — FFmpeg Integration via Docker

| File | Action |
|---|---|
| **New: `Dockerfile`** | .NET 9 + FFmpeg pre-installed |
| **New: `docker-compose.yml`** | API + Web + volume mounts |
| `Infrastructure.csproj` | Add `FFMpegCore` package |

### 4.2 — Video Converter

| File | Action |
|---|---|
| `Enums/FileFormat.cs` | Add: Mp4, Mkv, WebM, Avi, Mov, Flv, Wmv, Ts, M4v |
| **New: `VideoConverter.cs`** | FFMpegCore-based video conversion |
| `SupportedConversions.cs` | Add all video format routes |
| `FormatOptionsProvider.cs` | Video options: codec, resolution, bitrate, FPS, trim start/end |

Supported conversions:
- Any video → MP4, MKV, WebM, AVI, MOV, GIF (animated)
- Video → Audio extraction (MP4 → MP3)
- Video → Thumbnail image (screenshot at timestamp)

### 4.3 — Audio Converter

| File | Action |
|---|---|
| `Enums/FileFormat.cs` | Add: Mp3, Wav, Flac, Aac, Ogg, Wma, M4a, Opus |
| **New: `AudioConverter.cs`** | FFMpegCore-based audio conversion |
| `SupportedConversions.cs` | Add all audio format routes |
| `FormatOptionsProvider.cs` | Audio options: bitrate, sample rate, channels, trim |

### 4.4 — Video/Audio Tool Pages

**New: `Components/Pages/VideoTools.razor`**
- Compress, trim, resize, extract audio, GIF maker

**New: `Components/Pages/AudioTools.razor`**
- Compress, trim, merge, normalize volume

---

## Phase 5: SaaS Infrastructure (Week 7-9)

**Goal**: Production-ready, horizontally scalable, persistent.

### 5.1 — Database (EF Core)

| File | Action |
|---|---|
| **New: `Infrastructure/Data/AppDbContext.cs`** | EF Core DbContext for jobs, batches |
| **New: `Infrastructure/Data/EfJobTracker.cs`** | Replace InMemoryJobTracker |
| `Domain/Models/ConversionJob.cs` | Add EF-compatible properties |
| **New: migrations** | SQLite for dev, PostgreSQL for prod |

### 5.2 — Cloud Storage Abstraction

| File | Action |
|---|---|
| **New: `Infrastructure/Storage/LocalFileStorage.cs`** | Refactored TempFileStorageService |
| **New: `Infrastructure/Storage/AzureBlobStorage.cs`** | Azure Blob implementation |
| **New: `Infrastructure/Storage/S3Storage.cs`** | AWS S3 implementation |
| `IFileStorageService.cs` | Add `GetSignedDownloadUrl(path, expiry)` method |

### 5.3 — Message Queue

| File | Action |
|---|---|
| **New: `Infrastructure/Queue/ChannelJobQueue.cs`** | System.Threading.Channels (local) |
| **New: `Infrastructure/Queue/RabbitMqJobQueue.cs`** | RabbitMQ (distributed) |
| `Domain/Interfaces/IJobQueue.cs` | Abstract queue interface |

### 5.4 — Webhooks

| File | Action |
|---|---|
| `DTOs/ConvertRequest.cs` | Add optional `callbackUrl` field |
| `ConversionWorker.cs` | POST to callbackUrl on completion/failure |
| **New: `Infrastructure/Services/WebhookService.cs`** | Webhook delivery with retry |

### 5.5 — Rate Limiting & Security

| File | Action |
|---|---|
| `API/Program.cs` | Use .NET 9 built-in `AddRateLimiter()` with sliding window |
| `ConvertController.cs` | MIME type validation via magic bytes (not just extension) |
| `appsettings.json` | CORS allowlist (not AllowAll) |
| `API/Program.cs` | Add response compression, security headers |

### 5.6 — Observability

| File | Action |
|---|---|
| `API/Program.cs` | Add OpenTelemetry (traces + metrics) |
| All services | Structured logging with correlation IDs |
| **New: `/health/live`, `/health/ready`** | Kubernetes-ready health probes |
| `API/Program.cs` | Prometheus metrics endpoint |

---

## Phase 6: Auth, Billing & Growth (Week 9-12)

### 6.1 — Authentication
- ASP.NET Identity + JWT
- API key generation for programmatic access
- OAuth2 social login (Google, GitHub, Microsoft)
- User dashboard (conversion history, usage stats)

### 6.2 — Pricing Tiers (Stripe)
| Tier | Price | Limits |
|---|---|---|
| Free | $0 | 20 conversions/day, 100MB, no video |
| Pro | $9.99/mo | 500 conversions/day, 1GB, all formats |
| Business | $29.99/mo | Unlimited, 5GB, priority queue, API access, webhooks |

### 6.3 — SEO & Marketing
- Sitemap.xml auto-generated from all conversion routes
- robots.txt
- Open Graph meta tags per conversion page
- Blog section with conversion tips/tutorials
- Google Analytics / Plausible Analytics

### 6.4 — CDN & Performance
- CloudFlare or Azure CDN for static assets
- Edge caching for format pages
- Converted file delivery via CDN (signed URLs)

---

## Format Inventory — Complete Target List

### Images (30+ formats via Magick.NET)
PNG, JPG/JPEG, WebP, GIF, BMP, TIFF, SVG, ICO, HEIC/HEIF, AVIF, PSD, EPS, TGA, JP2/JPEG2000, JFIF, APNG, DDS, PCX, WBMP, PBM, PGM, PPM, XBM, XPM, DNG, CR2 (Canon), NEF (Nikon), ARW (Sony), ORF (Olympus), RAF (Fuji), RW2 (Panasonic)

### Documents (10 formats)
DOCX, DOC, ODT, RTF, TXT, HTML, Markdown, XML, EPUB, PDF

### Spreadsheets (5 formats)
XLSX, XLS, ODS, CSV, TSV

### Presentations (3 formats)
PPTX, PPT, ODP

### Video (10 formats, Phase 4)
MP4, MKV, WebM, AVI, MOV, FLV, WMV, TS, M4V, GIF(animated)

### Audio (8 formats, Phase 4)
MP3, WAV, FLAC, AAC, OGG, WMA, M4A, OPUS

### PDF Tools (not format conversion)
Merge, Split, Compress, Rotate, To-Images, Metadata, Watermark

### Image Tools (not format conversion)
Compress, Resize, Crop, Rotate, Watermark, Strip EXIF, Color adjust

---

## Success Metrics

| Metric | Current | Phase 1 | Phase 3 | Phase 6 |
|---|---|---|---|---|
| Supported formats | 22 | 22 (all working) | 50+ | 70+ |
| Conversion paths | ~80 | ~120 | ~500+ | ~800+ |
| Max file size | 50MB | 500MB | 1GB | 5GB (paid) |
| External deps | LibreOffice | NONE | NONE | FFmpeg (Docker) |
| PDF tools | Convert only | Convert only | Full suite | Full suite |
| Image tools | Convert only | Convert only | Full suite | Full suite |
| Video/Audio | None | None | None | Full suite |
| Auth | None | None | None | Full |
| Billing | None | None | None | Stripe |
| SEO pages | 0 | 0 | 500+ | 800+ |
| Docker | No | Yes | Yes | Yes |
| Tests | 29 | 60+ | 150+ | 300+ |

---

## Implementation Priority Order

```
Phase 1.1  Remove LibreOffice, add NPOI          ← START HERE
Phase 1.2  Fix all 11 bugs
Phase 1.3  Infrastructure fixes (health, config)
Phase 2.1  Magick.NET replaces ImageSharp
Phase 2.2  New image formats (HEIC, AVIF, PSD...)
Phase 2.3  PDF tools (merge, split, compress)
Phase 2.4  Image tools (compress, resize, crop)
Phase 2.5  Markdown support
Phase 3.1  SEO conversion pages
Phase 3.2  Home page redesign
Phase 3.3  SignalR real-time progress
Phase 3.4  Dark mode + responsive
Phase 3.5  PDF & Image tool pages
Phase 4    Video + Audio (Docker + FFmpeg)
Phase 5    DB + Cloud storage + Queues
Phase 6    Auth + Billing + CDN
```
