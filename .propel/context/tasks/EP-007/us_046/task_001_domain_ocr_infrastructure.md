# Task 001: Domain Extension + OCR Infrastructure (Tesseract + PdfPig)

## Context

| Field             | Value                                                                        |
|-------------------|------------------------------------------------------------------------------|
| **User Story**    | US-046                                                                       |
| **Epic**          | EP-007                                                                       |
| **Layer**         | Domain / Infrastructure                                                      |
| **Priority**      | Critical                                                                     |
| **Estimated Effort** | 75 minutes                                                                |
| **Dependencies**  | US-045 Task 001 complete — `ClinicalDocument` entity, `IDocumentStorageService`, `LocalDocumentStorageService` must exist |

## Objective

Extend the `ClinicalDocument` entity with OCR output fields (`ExtractedText` as
JSONB, `OcrConfidenceScore`), expose a `ReadAsync` decryption method on the
storage service interface, define the `IOcrService` contract in the Application
layer, and implement `TesseractOcrService` in Infrastructure using:

- **PdfPig** for embedded-text extraction from PDFs
- **PDFtoImage** to render scanned/image PDF pages to bitmaps for Tesseract
- **Tesseract.NET** for OCR on image files (PNG/JPG/TIFF) and scanned PDF pages

All processing is fully local — no external API calls (TR-026 / ADR-004).

## Acceptance Criteria Covered

- AC: Tesseract OCR engine used for text extraction
- AC: PDF — extract embedded text first; OCR only for scanned/image PDFs
- AC: Image documents (PNG/JPG/TIFF) — OCR applied directly
- AC: Extracted text stored in document record (JSONB field `extracted_text`)
- AC: OCR confidence score stored per page/region
- AC: All OCR processing runs locally (no external API calls)

---

## Implementation Steps

### 1. Add NuGet Packages to `HealthPlatform.Infrastructure`

Edit `src/HealthPlatform.Infrastructure/HealthPlatform.Infrastructure.csproj`.

Add inside the `<ItemGroup>` block that contains other `<PackageReference>` entries:

```xml
<PackageReference Include="Tesseract" Version="5.2.0" />
<PackageReference Include="UglyToad.PdfPig" Version="0.1.9" />
<PackageReference Include="PDFtoImage" Version="4.3.0" />
```

> **Note**: `Tesseract` 5.x wraps the native `libtesseract` library.
> The Dockerfile for the API project must install `tesseract-ocr` and the
> `tessdata` language packs (step covered in Task 003). For local development
> set the `Tesseract:TessDataPath` config key to the local tessdata directory.

---

### 2. Extend `ClinicalDocument` Entity

Edit `src/HealthPlatform.Domain/Entities/ClinicalDocument.cs`.

Add two new properties **after** `EncryptionIv`:

```csharp
using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

public class ClinicalDocument : AuditableEntity
{
    public Guid PatientId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public DocumentProcessingStatus ProcessingStatus { get; set; }

    /// <summary>
    /// Hex-encoded 16-byte AES-IV used to encrypt this file.
    /// The master encryption key lives in DocumentStorage:EncryptionKey (config).
    /// </summary>
    public string EncryptionIv { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of OcrPageResult objects: [{ pageNumber, text, confidenceScore }].
    /// Null until OCR job completes successfully.
    /// Stored as a PostgreSQL JSONB column for efficient querying.
    /// </summary>
    public string? ExtractedText { get; set; }

    /// <summary>
    /// Average OCR confidence score across all pages (0.0–100.0).
    /// Null until OCR job completes successfully.
    /// </summary>
    public double? OcrConfidenceScore { get; set; }

    public PatientProfile Patient { get; set; } = null!;
    public ICollection<ExtractedData> ExtractedData { get; set; } = [];
}
```

---

### 3. Update EF Configuration

Edit `src/HealthPlatform.Infrastructure/Persistence/Configurations/ClinicalDocumentConfiguration.cs`.

Add two property mappings **after** the `EncryptionIv` line:

```csharp
using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class ClinicalDocumentConfiguration : IEntityTypeConfiguration<ClinicalDocument>
{
    public void Configure(EntityTypeBuilder<ClinicalDocument> builder)
    {
        builder.HasKey(cd => cd.Id);

        builder.Property(cd => cd.FileName).IsRequired().HasMaxLength(500);
        builder.Property(cd => cd.MimeType).IsRequired().HasMaxLength(100);
        builder.Property(cd => cd.StoragePath).IsRequired().HasMaxLength(1000);
        builder.Property(cd => cd.FileSizeBytes).IsRequired();
        builder.Property(cd => cd.EncryptionIv).IsRequired().HasMaxLength(64);

        builder.Property(cd => cd.ExtractedText).HasColumnType("jsonb");
        builder.Property(cd => cd.OcrConfidenceScore);

        builder.Property(cd => cd.ProcessingStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(cd => cd.PatientId);

        builder.HasOne(cd => cd.Patient)
            .WithMany(p => p.ClinicalDocuments)
            .HasForeignKey(cd => cd.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

---

### 4. Add Migration SQL

Append to `infra/postgres/migrations.sql`:

```sql
-- US-046: Add extracted_text (JSONB) and ocr_confidence_score columns
DO $EF$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'clinical_documents' AND column_name = 'extracted_text'
    ) THEN
        ALTER TABLE clinical_documents
            ADD COLUMN extracted_text       jsonb            NULL,
            ADD COLUMN ocr_confidence_score double precision NULL;
    END IF;
END $EF$;
```

---

### 5. Create `OcrPageResult` Value Object

Create `src/HealthPlatform.Application/Features/Documents/OcrPageResult.cs`:

```csharp
namespace HealthPlatform.Application.Features.Documents;

/// <summary>
/// Represents the OCR output for a single page or region within a document.
/// Serialised to JSON and stored in <c>ClinicalDocument.ExtractedText</c>.
/// </summary>
public sealed record OcrPageResult(
    int    PageNumber,
    string Text,
    double ConfidenceScore
);
```

---

### 6. Extend `IDocumentStorageService` with `ReadAsync`

Edit `src/HealthPlatform.Application/Interfaces/IDocumentStorageService.cs`.

Add a `ReadAsync` method so the OCR handler can decrypt a stored file without
re-implementing AES logic:

```csharp
namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Persists and retrieves clinical document files.
/// All data is encrypted with AES-256-CBC before writing.
/// </summary>
public interface IDocumentStorageService
{
    /// <summary>
    /// Encrypts <paramref name="content"/> and writes it to the configured base path.
    /// </summary>
    /// <returns>
    /// A tuple of (storagePath, encryptionIv) where storagePath is the
    /// relative file path under BasePath and encryptionIv is the hex-encoded IV.
    /// </returns>
    Task<(string StoragePath, string EncryptionIv)> SaveAsync(
        string originalFileName,
        Stream content,
        CancellationToken ct);

    /// <summary>
    /// Decrypts the stored file and returns a readable in-memory stream.
    /// Caller is responsible for disposing the returned stream.
    /// </summary>
    Task<Stream> ReadAsync(string storagePath, string encryptionIv, CancellationToken ct);

    /// <summary>
    /// Deletes the stored file; best-effort, does not throw.
    /// </summary>
    void Delete(string storagePath);
}
```

---

### 7. Implement `ReadAsync` on `LocalDocumentStorageService`

Edit `src/HealthPlatform.Infrastructure/Documents/LocalDocumentStorageService.cs`.

Add the `ReadAsync` implementation below the existing `SaveAsync` method:

```csharp
public async Task<Stream> ReadAsync(string storagePath, string encryptionIv, CancellationToken ct)
{
    var settings   = _options.Value;
    var fullPath   = Path.Combine(settings.BasePath, storagePath);
    var masterKey  = Convert.FromBase64String(settings.EncryptionKey);
    var iv         = Convert.FromHexString(encryptionIv);

    var cipherBytes = await File.ReadAllBytesAsync(fullPath, ct);

    using var aes = Aes.Create();
    aes.Key  = masterKey;
    aes.IV   = iv;
    aes.Mode = CipherMode.CBC;

    using var decryptor = aes.CreateDecryptor();
    var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
    return new MemoryStream(plainBytes);
}
```

> **Note**: `Convert.FromHexString` requires .NET 5+; already satisfied by .NET 8.
> The returned `MemoryStream` is seekable — Tesseract.NET and PdfPig both require
> seekable streams.

---

### 8. Create `IOcrService` Interface

Create `src/HealthPlatform.Application/Interfaces/IOcrService.cs`:

```csharp
using HealthPlatform.Application.Features.Documents;

namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Extracts text from clinical documents using OCR.
/// All processing runs locally (TR-026 / ADR-004).
/// </summary>
public interface IOcrService
{
    /// <summary>
    /// Extracts text from the given document stream.
    /// </summary>
    /// <param name="fileStream">
    /// A readable, seekable stream of the decrypted document bytes.
    /// </param>
    /// <param name="mimeType">
    /// MIME type of the document (e.g., <c>application/pdf</c>, <c>image/png</c>).
    /// Determines whether PdfPig or Tesseract is used as the primary extractor.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A list of <see cref="OcrPageResult"/> — one entry per page/region.
    /// Never returns null; returns an empty list on total failure.
    /// </returns>
    Task<IReadOnlyList<OcrPageResult>> ExtractAsync(
        Stream fileStream,
        string mimeType,
        CancellationToken ct);
}
```

---

### 9. Create `TesseractSettings`

Create `src/HealthPlatform.Application/Settings/TesseractSettings.cs`:

```csharp
namespace HealthPlatform.Application.Settings;

public sealed class TesseractSettings
{
    public const string SectionName = "Tesseract";

    /// <summary>
    /// Absolute path to the directory containing Tesseract language data files
    /// (*.traineddata). Defaults to /usr/share/tessdata in Docker.
    /// Override with environment variable: Tesseract__TessDataPath
    /// </summary>
    public string TessDataPath { get; init; } = "/usr/share/tessdata";

    /// <summary>
    /// Tesseract language code to use (e.g., "eng" for English).
    /// </summary>
    public string Language { get; init; } = "eng";

    /// <summary>
    /// Minimum average confidence score (0–100) below which the document
    /// is flagged as low-quality and status is set to Failed.
    /// </summary>
    public double MinimumConfidenceThreshold { get; init; } = 30.0;

    /// <summary>
    /// Minimum character count of embedded PDF text before treating the PDF
    /// as text-based (and skipping OCR). PDFs with fewer characters are treated
    /// as scanned and passed to Tesseract.
    /// </summary>
    public int PdfEmbeddedTextMinLength { get; init; } = 50;
}
```

---

### 10. Add `TesseractSettings` to `appsettings.json` and `appsettings.Development.json`

Edit `src/HealthPlatform.Api/appsettings.json` — add inside the root JSON object:

```json
"Tesseract": {
  "TessDataPath": "/usr/share/tessdata",
  "Language": "eng",
  "MinimumConfidenceThreshold": 30.0,
  "PdfEmbeddedTextMinLength": 50
}
```

Edit `src/HealthPlatform.Api/appsettings.Development.json` — add inside the root JSON object
(update `TessDataPath` to your local Tesseract data directory):

```json
"Tesseract": {
  "TessDataPath": "C:/Program Files/Tesseract-OCR/tessdata",
  "Language": "eng",
  "MinimumConfidenceThreshold": 30.0,
  "PdfEmbeddedTextMinLength": 50
}
```

---

### 11. Implement `TesseractOcrService`

Create `src/HealthPlatform.Infrastructure/Documents/TesseractOcrService.cs`:

```csharp
using HealthPlatform.Application.Features.Documents;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Application.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PDFtoImage;
using Tesseract;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace HealthPlatform.Infrastructure.Documents;

/// <summary>
/// OCR service using Tesseract for images and PdfPig + PDFtoImage for PDFs.
/// For PDFs: embedded text is preferred; Tesseract is only applied when the
/// embedded text is too short (likely a scanned document).
/// </summary>
internal sealed class TesseractOcrService : IOcrService
{
    private readonly TesseractSettings                  _settings;
    private readonly ILogger<TesseractOcrService>       _logger;

    public TesseractOcrService(
        IOptions<TesseractSettings>           options,
        ILogger<TesseractOcrService>          logger)
    {
        _settings = options.Value;
        _logger   = logger;
    }

    public async Task<IReadOnlyList<OcrPageResult>> ExtractAsync(
        Stream     fileStream,
        string     mimeType,
        CancellationToken ct)
    {
        return mimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            ? await ExtractFromPdfAsync(fileStream, ct)
            : await ExtractFromImageAsync(fileStream, mimeType, ct);
    }

    // ── PDF extraction ───────────────────────────────────────────────────────

    private async Task<IReadOnlyList<OcrPageResult>> ExtractFromPdfAsync(
        Stream fileStream, CancellationToken ct)
    {
        await Task.Yield(); // keep async contract without forcing thread-pool allocation here

        // Step 1: attempt embedded text via PdfPig
        fileStream.Seek(0, SeekOrigin.Begin);
        var pdfBytes     = ReadAllBytes(fileStream);
        var embeddedText = ExtractEmbeddedPdfText(pdfBytes);

        if (embeddedText.Count > 0 &&
            embeddedText.Sum(p => p.Text.Length) >= _settings.PdfEmbeddedTextMinLength)
        {
            _logger.LogInformation(
                "PDF has sufficient embedded text ({TotalChars} chars) — skipping Tesseract.",
                embeddedText.Sum(p => p.Text.Length));
            return embeddedText;
        }

        // Step 2: scanned PDF — render each page to bitmap and run Tesseract
        _logger.LogInformation(
            "PDF embedded text too short; applying Tesseract OCR on rendered pages.");

        return await OcrRenderedPdfPagesAsync(pdfBytes, ct);
    }

    private static List<OcrPageResult> ExtractEmbeddedPdfText(byte[] pdfBytes)
    {
        var results = new List<OcrPageResult>();
        using var doc = PdfDocument.Open(pdfBytes);
        foreach (var page in doc.GetPages())
        {
            var text = string.Concat(page.GetWords().Select(w => w.Text + " ")).Trim();
            results.Add(new OcrPageResult(page.Number, text, 100.0)); // embedded = max confidence
        }
        return results;
    }

    private async Task<IReadOnlyList<OcrPageResult>> OcrRenderedPdfPagesAsync(
        byte[] pdfBytes, CancellationToken ct)
    {
        var results = new List<OcrPageResult>();
        var images  = Conversion.ToImages(pdfBytes);
        int pageNum = 1;

        await using var engine = CreateEngine();

        foreach (var image in images)
        {
            ct.ThrowIfCancellationRequested();

            // PDFtoImage returns System.Drawing.Bitmap-compatible SkiaSharp.SKBitmap
            var pngBytes = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100).ToArray();
            using var pixPng = Pix.LoadFromMemory(pngBytes);
            var result = RunTesseract(engine, pixPng, pageNum++);
            results.Add(result);
        }

        return results;
    }

    // ── Image extraction ─────────────────────────────────────────────────────

    private async Task<IReadOnlyList<OcrPageResult>> ExtractFromImageAsync(
        Stream fileStream, string mimeType, CancellationToken ct)
    {
        await Task.Yield();

        var imageBytes = ReadAllBytes(fileStream);
        using var engine = CreateEngine();
        using var pix     = Pix.LoadFromMemory(imageBytes);
        var result = RunTesseract(engine, pix, pageNumber: 1);
        return [result];
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private TesseractEngine CreateEngine() =>
        new(_settings.TessDataPath, _settings.Language, EngineMode.Default);

    private OcrPageResult RunTesseract(TesseractEngine engine, Pix pix, int pageNumber)
    {
        try
        {
            using var page = engine.Process(pix);
            var text       = page.GetText()?.Trim() ?? string.Empty;
            var confidence = (double)page.GetMeanConfidence() * 100.0;

            _logger.LogDebug(
                "OCR page {Page}: {CharCount} chars, confidence {Confidence:F1}%",
                pageNumber, text.Length, confidence);

            return new OcrPageResult(pageNumber, text, Math.Round(confidence, 2));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tesseract failed on page {Page}.", pageNumber);
            return new OcrPageResult(pageNumber, string.Empty, 0.0);
        }
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
```

---

### 12. Register Services in `DependencyInjection.cs`

Edit `src/HealthPlatform.Infrastructure/DependencyInjection.cs`.

Add the `using` statement:

```csharp
using HealthPlatform.Infrastructure.Documents;
```

And register the new services **after** the existing `IDocumentStorageService` registration:

```csharp
services.Configure<TesseractSettings>(
    configuration.GetSection(TesseractSettings.SectionName));
services.AddScoped<IOcrService, TesseractOcrService>();
```

---

## File Checklist

| File | Action |
|------|--------|
| `src/HealthPlatform.Infrastructure/HealthPlatform.Infrastructure.csproj` | Add 3 NuGet packages |
| `src/HealthPlatform.Domain/Entities/ClinicalDocument.cs` | Add `ExtractedText`, `OcrConfidenceScore` |
| `src/HealthPlatform.Infrastructure/Persistence/Configurations/ClinicalDocumentConfiguration.cs` | Add JSONB + double property config |
| `infra/postgres/migrations.sql` | Append US-046 migration block |
| `src/HealthPlatform.Application/Features/Documents/OcrPageResult.cs` | Create (new) |
| `src/HealthPlatform.Application/Interfaces/IDocumentStorageService.cs` | Add `ReadAsync` method |
| `src/HealthPlatform.Infrastructure/Documents/LocalDocumentStorageService.cs` | Implement `ReadAsync` |
| `src/HealthPlatform.Application/Interfaces/IOcrService.cs` | Create (new) |
| `src/HealthPlatform.Application/Settings/TesseractSettings.cs` | Create (new) |
| `src/HealthPlatform.Api/appsettings.json` | Add `Tesseract` section |
| `src/HealthPlatform.Api/appsettings.Development.json` | Add `Tesseract` section |
| `src/HealthPlatform.Infrastructure/Documents/TesseractOcrService.cs` | Create (new) |
| `src/HealthPlatform.Infrastructure/DependencyInjection.cs` | Register `IOcrService` |

## Definition of Done

- [ ] Solution builds with `dotnet build src/HealthPlatform.sln --configuration Release`
- [ ] `ClinicalDocument` has `ExtractedText` (nullable string, JSONB) and `OcrConfidenceScore` (nullable double)
- [ ] `IDocumentStorageService.ReadAsync` implemented and matches interface
- [ ] `IOcrService` resolves from DI without error
- [ ] `TesseractOcrService` returns `OcrPageResult` list for both PDF and image MIME types
- [ ] Migration SQL appended to `infra/postgres/migrations.sql`
