# Task 002: Application CQRS — Process OCR Command + Hangfire Job

## Context

| Field                | Value                                                                         |
|----------------------|-------------------------------------------------------------------------------|
| **User Story**       | US-046                                                                        |
| **Epic**             | EP-007                                                                        |
| **Layer**            | Application (CQRS) / Infrastructure (Hangfire Job)                           |
| **Priority**         | Critical                                                                      |
| **Estimated Effort** | 75 minutes                                                                    |
| **Dependencies**     | Task 001 complete — `IOcrService`, `IDocumentStorageService.ReadAsync`, `ClinicalDocument.ExtractedText`, `OcrPageResult` must exist |

## Objective

Implement the full OCR processing pipeline in the Application layer:

1. **`ProcessDocumentOcrCommand`** — triggers OCR for a single `ClinicalDocument`.
2. **`ProcessDocumentOcrCommandHandler`** — orchestrates: decrypt file → call
   `IOcrService` → persist `ExtractedText` + `OcrConfidenceScore` → update
   `ProcessingStatus` to `Processed` (or `Failed` on error).
3. **`GetDocumentOcrResultQuery`** + handler — returns the OCR result for a
   specific document with patient ownership check.
4. **`DocumentOcrResultDto`** — carries the OCR pages and aggregate confidence.
5. **`DocumentOcrJob`** — Hangfire fire-and-forget wrapper that receives a
   `Guid documentId` and dispatches `ProcessDocumentOcrCommand` via MediatR.
6. **Update `UploadDocumentCommandHandler`** — enqueue `DocumentOcrJob`
   immediately after the DB record is saved successfully.

## Acceptance Criteria Covered

- AC: OCR pipeline triggered automatically on document upload (via Hangfire)
- AC: Document status: Uploaded → Processing (during OCR) → Processed (on completion)
- AC: Failed OCR logged with error, document status → Failed
- AC: Extracted text stored in document record (JSONB field `extracted_text`)
- AC: OCR confidence score stored per page/region
- AC: Processing time target: <30 seconds for single-page document

---

## Implementation Steps

### 1. Create `DocumentOcrResultDto`

Create `src/HealthPlatform.Application/Features/Documents/DocumentOcrResultDto.cs`:

```csharp
namespace HealthPlatform.Application.Features.Documents;

/// <summary>
/// Carries the OCR extraction result for a single clinical document.
/// </summary>
public sealed record DocumentOcrResultDto(
    Guid                        DocumentId,
    string                      FileName,
    string                      ProcessingStatus,
    double?                     OcrConfidenceScore,
    IReadOnlyList<OcrPageResult> Pages
);
```

---

### 2. Create `ProcessDocumentOcrCommand`

Create `src/HealthPlatform.Application/Features/Documents/ProcessDocumentOcrCommand.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Documents;

/// <summary>
/// Triggers the OCR extraction pipeline for a single clinical document.
/// Dispatched by <see cref="DocumentOcrJob"/> after a successful upload.
/// </summary>
public sealed record ProcessDocumentOcrCommand(Guid DocumentId) : IRequest;
```

---

### 3. Create `ProcessDocumentOcrCommandHandler`

Create `src/HealthPlatform.Application/Features/Documents/ProcessDocumentOcrCommandHandler.cs`:

```csharp
using System.Text.Json;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Application.Settings;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthPlatform.Application.Features.Documents;

internal sealed class ProcessDocumentOcrCommandHandler
    : IRequestHandler<ProcessDocumentOcrCommand>
{
    private readonly IUnitOfWork                              _uow;
    private readonly IDocumentStorageService                  _storage;
    private readonly IOcrService                              _ocr;
    private readonly TesseractSettings                        _tesseractSettings;
    private readonly ILogger<ProcessDocumentOcrCommandHandler> _logger;

    public ProcessDocumentOcrCommandHandler(
        IUnitOfWork                              uow,
        IDocumentStorageService                  storage,
        IOcrService                              ocr,
        IOptions<TesseractSettings>              tesseractOptions,
        ILogger<ProcessDocumentOcrCommandHandler> logger)
    {
        _uow               = uow;
        _storage           = storage;
        _ocr               = ocr;
        _tesseractSettings = tesseractOptions.Value;
        _logger            = logger;
    }

    public async Task Handle(ProcessDocumentOcrCommand command, CancellationToken ct)
    {
        // 1. Load the document record
        var document = await _uow.Repository<ClinicalDocument>()
            .GetByIdAsync(command.DocumentId, ct)
            ?? throw new NotFoundException(nameof(ClinicalDocument), command.DocumentId);

        // 2. Transition: Uploaded → Processing
        document.ProcessingStatus = DocumentProcessingStatus.Processing;
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "OCR started for document {DocumentId} ({FileName}).",
            document.Id, document.FileName);

        try
        {
            // 3. Decrypt file from disk
            await using var stream = await _storage.ReadAsync(
                document.StoragePath, document.EncryptionIv, ct);

            // 4. Run OCR
            var pages = await _ocr.ExtractAsync(stream, document.MimeType, ct);

            // 5. Persist results
            var avgConfidence = pages.Count > 0
                ? pages.Average(p => p.ConfidenceScore)
                : 0.0;

            document.ExtractedText      = JsonSerializer.Serialize(pages);
            document.OcrConfidenceScore = Math.Round(avgConfidence, 2);

            // 6. Flag low-confidence documents as Failed
            if (avgConfidence < _tesseractSettings.MinimumConfidenceThreshold && pages.Count > 0)
            {
                _logger.LogWarning(
                    "Document {DocumentId} confidence {Score:F1}% is below threshold {Min:F1}%; marking Failed.",
                    document.Id, avgConfidence, _tesseractSettings.MinimumConfidenceThreshold);
                document.ProcessingStatus = DocumentProcessingStatus.Failed;
            }
            else
            {
                // 7. Transition: Processing → Processed
                document.ProcessingStatus = DocumentProcessingStatus.Processed;
            }

            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation(
                "OCR completed for document {DocumentId}: {PageCount} page(s), avg confidence {Confidence:F1}%.",
                document.Id, pages.Count, avgConfidence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "OCR failed for document {DocumentId}. Marking status as Failed.",
                document.Id);

            // Transition: Processing → Failed
            document.ProcessingStatus = DocumentProcessingStatus.Failed;
            await _uow.SaveChangesAsync(ct);
        }
    }
}
```

---

### 4. Create `GetDocumentOcrResultQuery`

Create `src/HealthPlatform.Application/Features/Documents/GetDocumentOcrResultQuery.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Documents;

/// <summary>
/// Returns the OCR result for a specific clinical document.
/// PatientId is used to verify ownership before returning data.
/// </summary>
public sealed record GetDocumentOcrResultQuery(
    Guid PatientId,
    Guid DocumentId
) : IRequest<DocumentOcrResultDto>;
```

---

### 5. Create `GetDocumentOcrResultQueryHandler`

Create `src/HealthPlatform.Application/Features/Documents/GetDocumentOcrResultQueryHandler.cs`:

```csharp
using System.Text.Json;
using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Application.Features.Documents;

internal sealed class GetDocumentOcrResultQueryHandler
    : IRequestHandler<GetDocumentOcrResultQuery, DocumentOcrResultDto>
{
    private readonly IUnitOfWork                               _uow;
    private readonly ILogger<GetDocumentOcrResultQueryHandler> _logger;

    public GetDocumentOcrResultQueryHandler(
        IUnitOfWork                               uow,
        ILogger<GetDocumentOcrResultQueryHandler> logger)
    {
        _uow    = uow;
        _logger = logger;
    }

    public async Task<DocumentOcrResultDto> Handle(
        GetDocumentOcrResultQuery query,
        CancellationToken ct)
    {
        // 1. Resolve PatientProfile.Id from User.Id (route param = User.Id)
        var profiles = await _uow.Repository<PatientProfile>()
            .GetAsync(new PatientProfileByUserIdSpecification(query.PatientId), ct);

        if (profiles.Count == 0)
            throw new NotFoundException(nameof(PatientProfile), query.PatientId);

        var profileId = profiles[0].Id;

        // 2. Load document and verify ownership
        var document = await _uow.Repository<ClinicalDocument>()
            .GetByIdAsync(query.DocumentId, ct)
            ?? throw new NotFoundException(nameof(ClinicalDocument), query.DocumentId);

        if (document.PatientId != profileId)
            throw new ForbiddenAccessException();

        // 3. Deserialise stored OCR pages
        IReadOnlyList<OcrPageResult> pages = [];
        if (!string.IsNullOrEmpty(document.ExtractedText))
        {
            pages = JsonSerializer.Deserialize<IReadOnlyList<OcrPageResult>>(
                document.ExtractedText) ?? [];
        }

        return new DocumentOcrResultDto(
            document.Id,
            document.FileName,
            document.ProcessingStatus.ToString(),
            document.OcrConfidenceScore,
            pages);
    }
}
```

> **Note**: `ForbiddenAccessException` is the project-standard exception for
> authorization failures — check `HealthPlatform.Domain/Common/Exceptions/` for
> the exact class name; rename if the project uses a different convention.

---

### 6. Create `DocumentOcrJob` Hangfire Job

Create `src/HealthPlatform.Infrastructure/Jobs/DocumentOcrJob.cs`:

```csharp
using HealthPlatform.Application.Features.Documents;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Infrastructure.Jobs;

/// <summary>
/// Hangfire fire-and-forget job that dispatches <see cref="ProcessDocumentOcrCommand"/>
/// for a single clinical document after a successful upload.
///
/// Enqueued by <c>UploadDocumentCommandHandler</c> via <see cref="IBackgroundJobClient"/>.
/// </summary>
public sealed class DocumentOcrJob
{
    private readonly IServiceScopeFactory     _scopeFactory;
    private readonly ILogger<DocumentOcrJob> _logger;

    public DocumentOcrJob(
        IServiceScopeFactory     scopeFactory,
        ILogger<DocumentOcrJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    /// <summary>
    /// Entry point invoked by Hangfire.
    /// </summary>
    public async Task ExecuteAsync(Guid documentId, CancellationToken ct = default)
    {
        _logger.LogInformation("DocumentOcrJob started for document {DocumentId}.", documentId);

        await using var scope  = _scopeFactory.CreateAsyncScope();
        var sender             = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new ProcessDocumentOcrCommand(documentId), ct);

        _logger.LogInformation("DocumentOcrJob completed for document {DocumentId}.", documentId);
    }
}
```

---

### 7. Register `DocumentOcrJob` in `DependencyInjection.cs`

Edit `src/HealthPlatform.Infrastructure/DependencyInjection.cs`.

Add inside the existing `services.AddTransient<...>` block (near `NoShowAutoMarkJob`):

```csharp
services.AddTransient<DocumentOcrJob>();
```

---

### 8. Update `UploadDocumentCommandHandler` to Enqueue OCR Job

Edit `src/HealthPlatform.Application/Features/Documents/UploadDocumentCommandHandler.cs`.

**Add constructor parameter** — inject `IBackgroundJobClient`:

```csharp
private readonly IUnitOfWork _uow;
private readonly IDocumentStorageService _storage;
private readonly ICurrentUserService _currentUser;
private readonly IBackgroundJobClient _jobClient;
private readonly ILogger<UploadDocumentCommandHandler> _logger;

public UploadDocumentCommandHandler(
    IUnitOfWork uow,
    IDocumentStorageService storage,
    ICurrentUserService currentUser,
    IBackgroundJobClient jobClient,
    ILogger<UploadDocumentCommandHandler> logger)
{
    _uow         = uow;
    _storage     = storage;
    _currentUser = currentUser;
    _jobClient   = jobClient;
    _logger      = logger;
}
```

**Add required `using`** at the top of the file:

```csharp
using Hangfire;
```

**After `await _uow.SaveChangesAsync(ct);`** (inside the `try` block, after audit log save),
enqueue the OCR job:

```csharp
await _uow.SaveChangesAsync(ct);

// Enqueue OCR job (fire-and-forget) — runs outside this HTTP request
_jobClient.Enqueue<DocumentOcrJob>(job => job.ExecuteAsync(document.Id, CancellationToken.None));
```

> **Placement**: The enqueue call goes **after** `SaveChangesAsync` succeeds, so the
> document ID is committed to the DB before the Hangfire worker picks up the job.
> The `catch` block (file cleanup) does not need to change — the job is never
> enqueued if the DB save fails.

---

## File Checklist

| File | Action |
|------|--------|
| `src/HealthPlatform.Application/Features/Documents/DocumentOcrResultDto.cs` | Create (new) |
| `src/HealthPlatform.Application/Features/Documents/ProcessDocumentOcrCommand.cs` | Create (new) |
| `src/HealthPlatform.Application/Features/Documents/ProcessDocumentOcrCommandHandler.cs` | Create (new) |
| `src/HealthPlatform.Application/Features/Documents/GetDocumentOcrResultQuery.cs` | Create (new) |
| `src/HealthPlatform.Application/Features/Documents/GetDocumentOcrResultQueryHandler.cs` | Create (new) |
| `src/HealthPlatform.Infrastructure/Jobs/DocumentOcrJob.cs` | Create (new) |
| `src/HealthPlatform.Infrastructure/DependencyInjection.cs` | Register `DocumentOcrJob` |
| `src/HealthPlatform.Application/Features/Documents/UploadDocumentCommandHandler.cs` | Inject `IBackgroundJobClient`, enqueue `DocumentOcrJob` |

## Definition of Done

- [ ] `ProcessDocumentOcrCommand` is handled; document status cycles through `Processing` → `Processed` (or `Failed`)
- [ ] `ExtractedText` and `OcrConfidenceScore` are persisted to the DB after successful OCR
- [ ] `GetDocumentOcrResultQuery` returns `DocumentOcrResultDto` with deserialized `Pages`
- [ ] `DocumentOcrJob` is registered in DI and enqueued by `UploadDocumentCommandHandler` after DB save
- [ ] `UploadDocumentCommandHandler` compiles — `IBackgroundJobClient` injected from Application.Interfaces namespace
- [ ] Solution builds with `dotnet build src/HealthPlatform.sln --configuration Release`
- [ ] Hangfire dashboard shows `DocumentOcrJob` in the queue after a test upload
