# Task 001: Add pageNumber to NerEntity + Raw Document Streaming Endpoint

## Context

| Field                | Value                                                                           |
|----------------------|---------------------------------------------------------------------------------|
| **User Story**       | US-048                                                                          |
| **Epic**             | EP-007                                                                          |
| **Layer**            | Domain / Application / Infrastructure / API / Python AI Service                |
| **Priority**         | Critical — Task 002 depends on pageNumber; Task 003 depends on raw endpoint    |
| **Estimated Effort** | 45 minutes                                                                      |
| **Dependencies**     | US-047 complete — `NerEntity` record, `entities` JSONB column, NER pipeline    |

## Objective

Two separate changes needed by the Angular viewer (Tasks 002 + 003):

1. **Add `pageNumber`** to `NerEntity` so the viewer can correctly attribute
   inline entity highlights to the right page. Currently the flat entity list
   has no page ownership information — offsets are per-page-relative but
   page membership is implicit in list ordering.

2. **Stream raw document** via a new `GET …/documents/{documentId}/raw`
   endpoint so the browser can render the original PDF or image in the
   side-by-side panel without a separate storage bucket or signed URL.

## Acceptance Criteria Covered

- AC: Side-by-side view — original document left (requires raw stream endpoint)
- AC: Highlights correct entities on the correct page (requires pageNumber field)

---

## Implementation Steps

### 1. Python AI Service — Add `page_number` to `EntitySpan`

Edit `src/ai-service/app/models/extraction_models.py`.

In `EntitySpan`, add after `low_confidence`:

```python
page_number: int = Field(description="1-based page index within the document.")
```

---

### 2. Python AI Service — Emit `page_number` in `ner_service.py`

Edit `src/ai-service/app/services/ner_service.py`.

In `extract_entities`, after adjusting offsets, set the page number:

```python
# BEFORE:
for ent in entities:
    ent["start_offset"] += char_offset
    ent["end_offset"]   += char_offset
results.extend(entities)
char_offset += len(chunk)

# AFTER:
for ent in entities:
    ent["start_offset"] += char_offset
    ent["end_offset"]   += char_offset
    ent["page_number"]  = page_index + 1   # 1-based, matches OcrPageResult.pageNumber
results.extend(entities)
char_offset += len(chunk)
```

---

### 3. C# — Add `PageNumber` to `NerEntity` Record

Edit `src/HealthPlatform.Application/Features/Documents/NerEntity.cs`.

Add `int PageNumber` as the last positional parameter:

```csharp
public sealed record NerEntity(
    string Text,
    string Type,
    int    StartOffset,
    int    EndOffset,
    double ConfidenceScore,
    bool   LowConfidence,

    /// <summary>1-based page number matching <see cref="OcrPageResult.PageNumber"/>.</summary>
    int PageNumber
);
```

---

### 4. C# — Update `AiServiceNerClient` to Map `page_number`

Edit `src/HealthPlatform.Infrastructure/Documents/AiServiceNerClient.cs`.

Add `page_number` to the private `NerEntityDto` record and pass it to the
`NerEntity` constructor:

```csharp
private sealed record NerEntityDto(
    [property: JsonPropertyName("text")]             string Text,
    [property: JsonPropertyName("type")]             string Type,
    [property: JsonPropertyName("start_offset")]     int    StartOffset,
    [property: JsonPropertyName("end_offset")]       int    EndOffset,
    [property: JsonPropertyName("confidence_score")] double ConfidenceScore,
    [property: JsonPropertyName("low_confidence")]   bool   LowConfidence,
    [property: JsonPropertyName("page_number")]      int    PageNumber        // ← new
);
```

Update the `.Select(...)` projection:

```csharp
return result.Entities
    .Select(e => new NerEntity(
        e.Text,
        e.Type,
        e.StartOffset,
        e.EndOffset,
        e.ConfidenceScore,
        e.LowConfidence,
        e.PageNumber))          // ← new
    .ToList();
```

> **Note:** `ProcessDocumentNerCommandHandler` uses `JsonSerializer.Serialize(entities)`,
> which will automatically include `PageNumber` in the JSONB because the record's
> property is now present. Existing stored entities without `page_number` will
> deserialize with `PageNumber = 0` (default int) — the Angular viewer should treat
> `pageNumber === 0` as "unknown page" and not filter.

---

### 5. C# — Create `GetDocumentRawFileQuery` + Handler

**Create** `src/HealthPlatform.Application/Features/Documents/GetDocumentRawFileQuery.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Documents;

/// <summary>
/// Returns the decrypted raw file stream for a clinical document.
/// Used by the Angular document viewer to display the original PDF or image.
/// </summary>
public sealed record GetDocumentRawFileQuery(Guid PatientId, Guid DocumentId)
    : IRequest<RawDocumentFile>;

/// <summary>Carries the decrypted file stream returned to the caller.</summary>
public sealed record RawDocumentFile(
    Stream FileStream,
    string ContentType,
    string FileName
);
```

**Create** `src/HealthPlatform.Application/Features/Documents/GetDocumentRawFileQueryHandler.cs`:

```csharp
using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Application.Features.Documents;

internal sealed class GetDocumentRawFileQueryHandler
    : IRequestHandler<GetDocumentRawFileQuery, RawDocumentFile>
{
    private readonly IUnitOfWork                               _uow;
    private readonly IDocumentStorageService                   _storage;
    private readonly ILogger<GetDocumentRawFileQueryHandler>   _logger;

    public GetDocumentRawFileQueryHandler(
        IUnitOfWork                             uow,
        IDocumentStorageService                 storage,
        ILogger<GetDocumentRawFileQueryHandler> logger)
    {
        _uow     = uow;
        _storage = storage;
        _logger  = logger;
    }

    public async Task<RawDocumentFile> Handle(
        GetDocumentRawFileQuery query,
        CancellationToken ct)
    {
        // 1. Resolve PatientProfile.Id from User.Id
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

        _logger.LogInformation(
            "Raw file stream requested for document {DocumentId}.", query.DocumentId);

        // 3. Decrypt and stream
        var stream = await _storage.ReadAsync(
            document.StoragePath, document.EncryptionIv, ct);

        return new RawDocumentFile(stream, document.MimeType, document.FileName);
    }
}
```

---

### 6. C# — Add API Endpoint in `PatientsController`

Edit `src/HealthPlatform.Api/Controllers/PatientsController.cs`.

Add the following action **before** the closing `}` of the class (after the existing
`GetDocumentOcrResult` action):

```csharp
/// <summary>
/// Streams the original (decrypted) clinical document file.
/// Used by the Angular document viewer to display the source PDF or image.
/// </summary>
/// <param name="patientId">Patient's User.Id (matches JWT sub for ownership check).</param>
/// <param name="documentId">The clinical document identifier.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>200 OK — raw file bytes with the document's original MIME type.</returns>
[HttpGet("{patientId:guid}/documents/{documentId:guid}/raw")]
[Authorize(Policy = PolicyNames.PatientOwnership)]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetDocumentRaw(
    Guid patientId,
    Guid documentId,
    CancellationToken ct)
{
    var result = await _sender.Send(new GetDocumentRawFileQuery(patientId, documentId), ct);
    return File(result.FileStream, result.ContentType, result.FileName);
}
```

> **Security note:** The `PatientOwnership` policy ensures only the document's
> owner can download it. The `File(...)` result sets `Content-Disposition: attachment`
> automatically with the file name. The stream is decrypted server-side — no
> encryption key is exposed to the browser (OWASP A02).

---

## File Checklist

| File                                                                               | Action |
|------------------------------------------------------------------------------------|--------|
| `src/ai-service/app/models/extraction_models.py`                                   | Modify — add `page_number` to `EntitySpan` |
| `src/ai-service/app/services/ner_service.py`                                       | Modify — emit `page_number` in `extract_entities` |
| `src/HealthPlatform.Application/Features/Documents/NerEntity.cs`                   | Modify — add `int PageNumber` parameter |
| `src/HealthPlatform.Infrastructure/Documents/AiServiceNerClient.cs`                | Modify — map `page_number` in DTO + constructor |
| `src/HealthPlatform.Application/Features/Documents/GetDocumentRawFileQuery.cs`     | Create |
| `src/HealthPlatform.Application/Features/Documents/GetDocumentRawFileQueryHandler.cs` | Create |
| `src/HealthPlatform.Api/Controllers/PatientsController.cs`                         | Modify — add `GetDocumentRaw` action |

## Verification

```bash
# .NET build — 0 errors expected
dotnet build src/HealthPlatform.sln --configuration Release

# Smoke test (with running stack):
# curl -H "Authorization: Bearer <token>" \
#   http://localhost:5000/api/patients/{userId}/documents/{docId}/raw \
#   --output test.pdf
# open test.pdf  # should open the original uploaded document

# Python syntax check
python -m py_compile src/ai-service/app/models/extraction_models.py
python -m py_compile src/ai-service/app/services/ner_service.py
```
