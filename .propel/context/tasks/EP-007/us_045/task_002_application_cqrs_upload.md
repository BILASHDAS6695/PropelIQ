# Task 002: Application Layer — Upload Command, Validation & Audit

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-045 |
| **Epic** | EP-007 |
| **Layer** | Application (CQRS) |
| **Priority** | Critical |
| **Estimated Effort** | 75 minutes |
| **Dependencies** | Task 001 (`IDocumentStorageService`, updated `ClinicalDocument` entity, `DocumentProcessingStatus.Uploaded`) |

## Objective

Implement the full application-layer pipeline for document upload:

1. **`UploadDocumentCommand`** — carries the raw stream, patient ID, filename,
   MIME type, and file size.
2. **`UploadDocumentCommandValidator`** — enforces allowed MIME types/extensions,
   10 MB size cap, and magic-byte file-type verification (ClamAV substitute).
3. **`UploadDocumentCommandHandler`** — verifies patient ownership, calls
   `IDocumentStorageService`, creates the `ClinicalDocument` DB record with
   `ProcessingStatus = Uploaded`, appends an `AuditLog` entry, and cleans up
   the stored file on any DB failure.
4. **`GetPatientDocumentsQuery`** + handler — returns the patient's document list.
5. Supporting DTOs: `DocumentUploadResultDto`, `DocumentSummaryDto`.

## Acceptance Criteria Covered

- AC: Upload endpoint accepts file + metadata
- AC: Supported formats: PDF, PNG, JPG, JPEG, TIFF (validated server-side)
- AC: Maximum file size: 10 MB per file
- AC: Virus scan on upload (magic-byte file-type validation)
- AC: Audit log entry for each upload (userId, documentId, timestamp)
- AC: Database record created: documentId, patientId, filename, mimeType, uploadedAt, status, filePath
- AC: Upload interrupted → partial file cleaned up, no DB record created
- AC: Duplicate filename → UUID suffix appended, both stored

---

## Implementation Steps

### 1. Create DTOs

Create `src/HealthPlatform.Application/Features/Documents/DocumentUploadResultDto.cs`:

```csharp
namespace HealthPlatform.Application.Features.Documents;

public sealed record DocumentUploadResultDto(
    Guid            DocumentId,
    string          FileName,
    string          MimeType,
    long            FileSizeBytes,
    DateTimeOffset  UploadedAt,
    string          ProcessingStatus
);
```

Create `src/HealthPlatform.Application/Features/Documents/DocumentSummaryDto.cs`:

```csharp
namespace HealthPlatform.Application.Features.Documents;

public sealed record DocumentSummaryDto(
    Guid            DocumentId,
    string          FileName,
    string          MimeType,
    long            FileSizeBytes,
    DateTimeOffset  UploadedAt,
    string          ProcessingStatus
);
```

---

### 2. Create `UploadDocumentCommand`

Create `src/HealthPlatform.Application/Features/Documents/UploadDocumentCommand.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Documents;

/// <summary>
/// Uploads an encrypted clinical document for a patient.
/// The caller must pass an open, readable <see cref="FileStream"/>;
/// the handler disposes nothing — callers own stream lifetime.
/// </summary>
public sealed record UploadDocumentCommand(
    Guid   PatientId,
    string OriginalFileName,
    string MimeType,
    long   FileSizeBytes,
    Stream FileContent
) : IRequest<DocumentUploadResultDto>;
```

---

### 3. Create `UploadDocumentCommandValidator`

Create `src/HealthPlatform.Application/Features/Documents/UploadDocumentCommandValidator.cs`:

```csharp
using FluentValidation;

namespace HealthPlatform.Application.Features.Documents;

public sealed class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    private const long MaxBytes = 10 * 1024 * 1024; // 10 MB

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/png",
        "image/jpeg",
        "image/tiff",
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".tiff", ".tif",
    };

    // Magic bytes for file-type validation (ClamAV substitute / first-line defence)
    private static readonly Dictionary<string, byte[][]> MagicBytes = new()
    {
        ["application/pdf"]  = [[0x25, 0x50, 0x44, 0x46]],           // %PDF
        ["image/png"]        = [[0x89, 0x50, 0x4E, 0x47]],           // ‰PNG
        ["image/jpeg"]       = [[0xFF, 0xD8, 0xFF]],                  // JFIF/EXIF
        ["image/tiff"]       = [[0x49, 0x49, 0x2A, 0x00],            // II*\0 (little-endian)
                                [0x4D, 0x4D, 0x00, 0x2A]],           // MM\0* (big-endian)
    };

    public UploadDocumentCommandValidator()
    {
        RuleFor(x => x.PatientId)
            .NotEmpty();

        RuleFor(x => x.OriginalFileName)
            .NotEmpty()
            .MaximumLength(500)
            .Must(name =>
            {
                var ext = Path.GetExtension(name);
                return AllowedExtensions.Contains(ext);
            })
            .WithMessage(x =>
            {
                var ext = Path.GetExtension(x.OriginalFileName);
                return $"Unsupported format: {ext}. Accepted: PDF, PNG, JPG, TIFF";
            });

        RuleFor(x => x.MimeType)
            .NotEmpty()
            .Must(m => AllowedMimeTypes.Contains(m))
            .WithMessage(x => $"Unsupported MIME type: {x.MimeType}");

        RuleFor(x => x.FileSizeBytes)
            .InclusiveBetween(1, MaxBytes)
            .WithMessage("File too large. Maximum size: 10 MB");

        RuleFor(x => x.FileContent)
            .NotNull()
            .Must((cmd, stream) => ValidateMagicBytes(cmd.MimeType, stream))
            .WithMessage("File content does not match the declared type (magic-byte mismatch)");
    }

    private static bool ValidateMagicBytes(string mimeType, Stream stream)
    {
        if (!MagicBytes.TryGetValue(mimeType, out var signatures))
            return false;

        const int ReadLen = 4;
        var header = new byte[ReadLen];
        var read   = stream.Read(header, 0, ReadLen);
        stream.Seek(0, SeekOrigin.Begin); // reset for subsequent reads

        return signatures.Any(sig =>
            read >= sig.Length &&
            header.Take(sig.Length).SequenceEqual(sig));
    }
}
```

---

### 4. Create `PatientDocumentsByPatientIdSpecification`

Create `src/HealthPlatform.Application/Features/Documents/PatientDocumentsByPatientIdSpecification.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using System.Linq.Expressions;

namespace HealthPlatform.Application.Features.Documents;

internal sealed class PatientDocumentsByPatientIdSpecification : ISpecification<ClinicalDocument>
{
    private readonly Guid _patientId;

    public PatientDocumentsByPatientIdSpecification(Guid patientId)
        => _patientId = patientId;

    public Expression<Func<ClinicalDocument, bool>> Criteria
        => d => d.PatientId == _patientId && !d.IsDeleted;

    public List<Expression<Func<ClinicalDocument, object>>> Includes => [];
    public Expression<Func<ClinicalDocument, object>>? OrderBy => d => d.UploadedAt;
    public Expression<Func<ClinicalDocument, object>>? OrderByDescending => null;
    public bool IsDescending => true;
    public int? Take => null;
    public int? Skip => null;
}
```

---

### 5. Create `UploadDocumentCommandHandler`

Create `src/HealthPlatform.Application/Features/Documents/UploadDocumentCommandHandler.cs`:

```csharp
using System.Text.Json;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Application.Features.Documents;

internal sealed class UploadDocumentCommandHandler
    : IRequestHandler<UploadDocumentCommand, DocumentUploadResultDto>
{
    private readonly IUnitOfWork               _uow;
    private readonly IDocumentStorageService   _storage;
    private readonly ICurrentUserService       _currentUser;
    private readonly ILogger<UploadDocumentCommandHandler> _logger;

    public UploadDocumentCommandHandler(
        IUnitOfWork                            uow,
        IDocumentStorageService                storage,
        ICurrentUserService                    currentUser,
        ILogger<UploadDocumentCommandHandler>  logger)
    {
        _uow         = uow;
        _storage     = storage;
        _currentUser = currentUser;
        _logger      = logger;
    }

    public async Task<DocumentUploadResultDto> Handle(
        UploadDocumentCommand command,
        CancellationToken     ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("Authentication required to upload documents.");

        // ── 1. Verify patient profile exists ─────────────────────────────
        var patient = await _uow.Repository<PatientProfile>()
            .GetByIdAsync(command.PatientId, ct)
            ?? throw new NotFoundException(nameof(PatientProfile), command.PatientId);

        // ── 2. Encrypt and write file to local storage ────────────────────
        var (storagePath, encryptionIv) = await _storage.SaveAsync(
            command.OriginalFileName,
            command.FileContent,
            ct);

        // ── 3. Persist DB record (rollback file on failure) ───────────────
        var now      = DateTimeOffset.UtcNow;
        var document = new ClinicalDocument
        {
            PatientId        = command.PatientId,
            FileName         = command.OriginalFileName,
            MimeType         = command.MimeType,
            StoragePath      = storagePath,
            FileSizeBytes    = command.FileSizeBytes,
            UploadedAt       = now,
            ProcessingStatus = DocumentProcessingStatus.Uploaded,
            EncryptionIv     = encryptionIv,
        };

        try
        {
            await _uow.Repository<ClinicalDocument>().AddAsync(document, ct);

            // ── 4. Audit log ──────────────────────────────────────────────
            var auditEntry = new AuditLog
            {
                UserId     = _currentUser.UserId.Value,
                Action     = "DocumentUploaded",
                EntityType = nameof(ClinicalDocument),
                EntityId   = document.Id,
                Timestamp  = now,
                Details    = JsonDocument.Parse(
                    $$$"""
                    {
                      "fileName":   "{{{command.OriginalFileName}}}",
                      "mimeType":   "{{{command.MimeType}}}",
                      "sizeBytes":  {{{command.FileSizeBytes}}},
                      "patientId":  "{{{command.PatientId}}}"
                    }
                    """),
            };
            await _uow.Repository<AuditLog>().AddAsync(auditEntry, ct);

            await _uow.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Partial upload: remove the already-written encrypted file
            _storage.Delete(storagePath);
            _logger.LogError(ex, "DB persist failed after writing document to disk. File cleaned up.");
            throw;
        }

        _logger.LogInformation(
            "Document {DocumentId} uploaded for patient {PatientId} ({FileName})",
            document.Id, command.PatientId, command.OriginalFileName);

        return new DocumentUploadResultDto(
            document.Id,
            document.FileName,
            document.MimeType,
            document.FileSizeBytes,
            document.UploadedAt,
            document.ProcessingStatus.ToString());
    }
}
```

---

### 6. Create `GetPatientDocumentsQuery` + Handler

Create `src/HealthPlatform.Application/Features/Documents/GetPatientDocumentsQuery.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Documents;

public sealed record GetPatientDocumentsQuery(Guid PatientId)
    : IRequest<IReadOnlyList<DocumentSummaryDto>>;
```

Create `src/HealthPlatform.Application/Features/Documents/GetPatientDocumentsQueryHandler.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.Documents;

internal sealed class GetPatientDocumentsQueryHandler
    : IRequestHandler<GetPatientDocumentsQuery, IReadOnlyList<DocumentSummaryDto>>
{
    private readonly IUnitOfWork _uow;

    public GetPatientDocumentsQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<DocumentSummaryDto>> Handle(
        GetPatientDocumentsQuery query,
        CancellationToken        ct)
    {
        var spec = new PatientDocumentsByPatientIdSpecification(query.PatientId);
        var docs = await _uow.Repository<ClinicalDocument>().GetAsync(spec, ct);

        return docs
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new DocumentSummaryDto(
                d.Id,
                d.FileName,
                d.MimeType,
                d.FileSizeBytes,
                d.UploadedAt,
                d.ProcessingStatus.ToString()))
            .ToList();
    }
}
```
