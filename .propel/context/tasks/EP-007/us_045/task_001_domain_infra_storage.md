# Task 001: Domain Extension + Local Storage Service (AES-256)

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-045 |
| **Epic** | EP-007 |
| **Layer** | Domain / Infrastructure |
| **Priority** | Critical |
| **Estimated Effort** | 60 minutes |
| **Dependencies** | None — greenfield on existing `ClinicalDocument` entity stub |

## Objective

Extend the existing `ClinicalDocument` domain entity with the fields required by
US-045 (`MimeType`, `EncryptionIv`), align the `DocumentProcessingStatus` enum
with the mandated lifecycle (`Uploaded → Processing → Processed → Verified`),
create the `IDocumentStorageService` interface and a concrete
`LocalDocumentStorageService` that writes AES-256-CBC-encrypted files to a
configurable local path, and register everything in the DI container.

## Acceptance Criteria Covered

- AC: File stored on local filesystem (configurable path via environment variable)
- AC: Files encrypted at rest (AES-256)
- AC: Database record: documentId, patientId, filename, mimeType, uploadedAt, status, filePath
- AC: Document status lifecycle: Uploaded → Processing → Processed → Verified

---

## Implementation Steps

### 1. Update `DocumentProcessingStatus` enum

Edit `src/HealthPlatform.Domain/Enums/DocumentProcessingStatus.cs`.

Replace the existing body to align with the US-045 lifecycle:

```csharp
namespace HealthPlatform.Domain.Enums;

public enum DocumentProcessingStatus
{
    /// <summary>File received, written to disk, DB record created.</summary>
    Uploaded,

    /// <summary>AI / NER extraction in progress.</summary>
    Processing,

    /// <summary>Extraction complete; awaiting clinician verification.</summary>
    Processed,

    /// <summary>Clinician has verified the extracted data.</summary>
    Verified,

    /// <summary>Upload, extraction, or verification failed.</summary>
    Failed,
}
```

> **Note**: `Pending` and `Completed` are removed. The stored string value
> changes, so add the migration column below. No existing production data exists
> at this stage.

---

### 2. Extend `ClinicalDocument` entity

Edit `src/HealthPlatform.Domain/Entities/ClinicalDocument.cs`.

Add two new properties **after** `ProcessingStatus`:

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

    public PatientProfile Patient { get; set; } = null!;
    public ICollection<ExtractedData> ExtractedData { get; set; } = [];
}
```

---

### 3. Update EF Configuration

Edit `src/HealthPlatform.Infrastructure/Persistence/Configurations/ClinicalDocumentConfiguration.cs`.

Add property configurations for the two new columns:

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
-- US-045: Add MimeType and EncryptionIv columns to clinical_documents
-- Align DocumentProcessingStatus values with new lifecycle
DO $EF$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'clinical_documents' AND column_name = 'mime_type'
    ) THEN
        ALTER TABLE clinical_documents ADD COLUMN mime_type character varying(100) NOT NULL DEFAULT '';
        ALTER TABLE clinical_documents ADD COLUMN encryption_iv character varying(64) NOT NULL DEFAULT '';
    END IF;
END $EF$;
```

---

### 5. Create `DocumentStorageSettings`

Create `src/HealthPlatform.Application/Settings/DocumentStorageSettings.cs`:

```csharp
namespace HealthPlatform.Application.Settings;

public sealed class DocumentStorageSettings
{
    public const string SectionName = "DocumentStorage";

    /// <summary>Root directory for encrypted document files.</summary>
    public string BasePath { get; init; } = "documents";

    /// <summary>
    /// Base64-encoded 32-byte master AES-256 encryption key.
    /// Rotate via environment variable: DocumentStorage__EncryptionKey
    /// </summary>
    public string EncryptionKey { get; init; } = string.Empty;
}
```

---

### 6. Create `IDocumentStorageService` Interface

Create `src/HealthPlatform.Application/Interfaces/IDocumentStorageService.cs`:

```csharp
namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Persists clinical document files to a configured storage backend.
/// All data is encrypted with AES-256-CBC before writing.
/// </summary>
public interface IDocumentStorageService
{
    /// <summary>
    /// Encrypts <paramref name="content"/> and writes it to the storage backend.
    /// </summary>
    /// <param name="originalFileName">
    ///   The original upload filename. Used to derive the stored filename.
    ///   If a file with the same name exists, a UUID suffix is appended.
    /// </param>
    /// <param name="content">Raw (unencrypted) file stream.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    ///   A tuple of the <c>storagePath</c> (relative to BasePath) and the
    ///   hex-encoded <c>encryptionIv</c> needed to decrypt the file.
    /// </returns>
    Task<(string StoragePath, string EncryptionIv)> SaveAsync(
        string originalFileName,
        Stream content,
        CancellationToken ct);

    /// <summary>
    /// Deletes the encrypted file at <paramref name="storagePath"/> (best-effort).
    /// Used for cleanup when DB persistence fails after a successful file write.
    /// </summary>
    void Delete(string storagePath);
}
```

---

### 7. Create `LocalDocumentStorageService`

Create `src/HealthPlatform.Infrastructure/Documents/LocalDocumentStorageService.cs`:

```csharp
using System.Security.Cryptography;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Application.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthPlatform.Infrastructure.Documents;

internal sealed class LocalDocumentStorageService : IDocumentStorageService
{
    private readonly DocumentStorageSettings            _settings;
    private readonly ILogger<LocalDocumentStorageService> _logger;

    public LocalDocumentStorageService(
        IOptions<DocumentStorageSettings>            options,
        ILogger<LocalDocumentStorageService>         logger)
    {
        _settings = options.Value;
        _logger   = logger;
    }

    public async Task<(string StoragePath, string EncryptionIv)> SaveAsync(
        string            originalFileName,
        Stream            content,
        CancellationToken ct)
    {
        Directory.CreateDirectory(_settings.BasePath);

        // Derive a unique filename to avoid collisions
        var ext      = Path.GetExtension(originalFileName);
        var baseName = Path.GetFileNameWithoutExtension(originalFileName);
        var stored   = $"{baseName}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(_settings.BasePath, stored);

        // Generate random 16-byte IV; master key comes from config
        var masterKey = Convert.FromBase64String(_settings.EncryptionKey);
        using var iv  = RandomNumberGenerator.GetBytes(16);
        var ivHex     = Convert.ToHexString(iv);

        using var aes       = Aes.Create();
        aes.Key             = masterKey;
        aes.IV              = iv;
        aes.Mode            = CipherMode.CBC;
        aes.Padding         = PaddingMode.PKCS7;
        using var encryptor = aes.CreateEncryptor();

        await using var fileStream    = new FileStream(fullPath, FileMode.Create, FileAccess.Write,
                                            FileShare.None, 81920, useAsync: true);
        await using var cryptoStream  = new CryptoStream(fileStream, encryptor, CryptoStreamMode.Write);
        await content.CopyToAsync(cryptoStream, ct);
        await cryptoStream.FlushFinalBlockAsync(ct);

        _logger.LogInformation("Document encrypted and saved to {StoredPath}", stored);
        return (stored, ivHex);
    }

    public void Delete(string storagePath)
    {
        var fullPath = Path.Combine(_settings.BasePath, storagePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("Deleted orphaned document at {Path}", storagePath);
        }
    }
}
```

---

### 8. Update `appsettings.json`

In `src/HealthPlatform.Api/appsettings.json`, add a new top-level section:

```json
"DocumentStorage": {
  "BasePath": "/app/documents",
  "EncryptionKey": "CHANGE-ME-USE-SECRET-MANAGER-32B-BASE64=="
}
```

In `src/HealthPlatform.Api/appsettings.Development.json`, override `BasePath` to
a local directory:

```json
"DocumentStorage": {
  "BasePath": "C:/tmp/healthplatform-documents",
  "EncryptionKey": "CHANGE-ME-USE-SECRET-MANAGER-32B-BASE64=="
}
```

> **Security note**: In production, `EncryptionKey` must be injected via an
> environment variable (`DocumentStorage__EncryptionKey`) or a secrets manager —
> never committed to source control.

---

### 9. Register in DI

In `src/HealthPlatform.Infrastructure/DependencyInjection.cs`, add inside
`AddInfrastructure()` after the existing registrations:

```csharp
services.Configure<DocumentStorageSettings>(
    configuration.GetSection(DocumentStorageSettings.SectionName));
services.AddScoped<IDocumentStorageService, LocalDocumentStorageService>();
```

Add the `using` at the top of the file:

```csharp
using HealthPlatform.Infrastructure.Documents;
```
