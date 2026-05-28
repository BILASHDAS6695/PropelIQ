# Task 003: .NET CQRS + Infrastructure + Angular UI (NER Pipeline Integration)

## Context

| Field                | Value                                                                                   |
|----------------------|-----------------------------------------------------------------------------------------|
| **User Story**       | US-047                                                                                  |
| **Epic**             | EP-007                                                                                  |
| **Layer**            | Application / Infrastructure / API / Angular                                            |
| **Priority**         | Critical                                                                                |
| **Estimated Effort** | 120 minutes                                                                             |
| **Dependencies**     | Task 001 + Task 002 complete — `NerEntity` record, `ClinicalDocument.Entities`, AI service `/extraction/ner` implemented |

## Objective

Chain NER processing after OCR in the Hangfire pipeline:

```
UploadDocumentCommandHandler
  → enqueue DocumentOcrJob
      → ProcessDocumentOcrCommandHandler (stores ExtractedText, stays Processing)
          → enqueue DocumentNerJob
              → ProcessDocumentNerCommandHandler
                  → AiServiceNerClient (HTTP POST /extraction/ner)
                      → stores Entities JSON, status → Processed
```

Also expose the entities in the existing `GetDocumentOcrResultQuery` response
and display them in the Angular `DocumentDetailComponent`.

## Acceptance Criteria Covered

- AC: NER pipeline runs after successful OCR (chained processing)
- AC: Entities stored as JSONB array in document record
- AC: Model unavailable → queue for retry, document status remains "Processing"
- AC: No entities found → empty array, document still marked "Processed"
- AC: NER results surfaced to Angular document detail screen

---

## Implementation Steps

### 1. Create `INerClient` Interface

Create `src/HealthPlatform.Application/Interfaces/INerClient.cs`:

```csharp
using HealthPlatform.Application.Features.Documents;

namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Sends page texts to the Python AI service for NER extraction.
/// Implementations live in the Infrastructure layer (ADR-004).
/// </summary>
public interface INerClient
{
    /// <summary>
    /// Extracts named entities from the provided page texts.
    /// </summary>
    /// <param name="pages">One entry per OCR-extracted document page.</param>
    /// <param name="confidenceThreshold">Entities below this score are flagged low_confidence.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Flat list of all entities across all pages.</returns>
    /// <exception cref="NerModelUnavailableException">
    /// Thrown when the AI service returns 503 — the Hangfire job should retry.
    /// </exception>
    Task<IReadOnlyList<NerEntity>> ExtractAsync(
        IReadOnlyList<string> pages,
        double confidenceThreshold,
        CancellationToken ct);
}
```

---

### 2. Create `NerModelUnavailableException`

Create `src/HealthPlatform.Domain/Common/Exceptions/NerModelUnavailableException.cs`:

```csharp
namespace HealthPlatform.Domain.Common.Exceptions;

/// <summary>
/// Thrown when the Python AI service NER model is temporarily unavailable (HTTP 503).
/// The Hangfire worker will automatically retry the job.
/// </summary>
public sealed class NerModelUnavailableException : Exception
{
    public NerModelUnavailableException()
        : base("NER model is unavailable. The job will be retried automatically.") { }

    public NerModelUnavailableException(string message) : base(message) { }

    public NerModelUnavailableException(string message, Exception innerException)
        : base(message, innerException) { }
}
```

---

### 3. Create `NerSettings`

Create `src/HealthPlatform.Application/Settings/NerSettings.cs`:

```csharp
namespace HealthPlatform.Application.Settings;

public sealed class NerSettings
{
    public const string SectionName = "Ner";

    /// <summary>Base URL of the Python AI service (e.g., http://ai:8000).</summary>
    public string AiServiceBaseUrl { get; init; } = "http://ai:8000";

    /// <summary>Internal API key sent in the X-Internal-Api-Key header.</summary>
    public string InternalApiKey { get; init; } = string.Empty;

    /// <summary>Minimum confidence threshold (0.0–1.0) sent to the NER service.</summary>
    public double ConfidenceThreshold { get; init; } = 0.7;

    /// <summary>HTTP request timeout for the NER call in seconds.</summary>
    public int TimeoutSeconds { get; init; } = 60;
}
```

---

### 4. Create `ProcessDocumentNerCommand`

Create `src/HealthPlatform.Application/Features/Documents/ProcessDocumentNerCommand.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Documents;

/// <summary>
/// Triggers the NER extraction pipeline for a single clinical document.
/// Dispatched by <see cref="HealthPlatform.Infrastructure.Jobs.DocumentNerJob"/>
/// after successful OCR processing.
/// </summary>
public sealed record ProcessDocumentNerCommand(Guid DocumentId) : IRequest;
```

---

### 5. Create `ProcessDocumentNerCommandHandler`

Create `src/HealthPlatform.Application/Features/Documents/ProcessDocumentNerCommandHandler.cs`:

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

internal sealed class ProcessDocumentNerCommandHandler : IRequestHandler<ProcessDocumentNerCommand>
{
    private readonly IUnitOfWork                               _uow;
    private readonly INerClient                                _nerClient;
    private readonly NerSettings                               _settings;
    private readonly ILogger<ProcessDocumentNerCommandHandler> _logger;

    public ProcessDocumentNerCommandHandler(
        IUnitOfWork                               uow,
        INerClient                                nerClient,
        IOptions<NerSettings>                     settings,
        ILogger<ProcessDocumentNerCommandHandler> logger)
    {
        _uow       = uow;
        _nerClient = nerClient;
        _settings  = settings.Value;
        _logger    = logger;
    }

    public async Task Handle(ProcessDocumentNerCommand command, CancellationToken ct)
    {
        var document = await _uow.Repository<ClinicalDocument>()
            .GetByIdAsync(command.DocumentId, ct);

        if (document is null)
        {
            _logger.LogWarning("NER skipped — document {DocumentId} not found.", command.DocumentId);
            return;
        }

        // If OCR failed, NER should not run.
        if (document.ProcessingStatus == DocumentProcessingStatus.Failed)
        {
            _logger.LogWarning(
                "NER skipped — document {DocumentId} is in Failed state.", command.DocumentId);
            return;
        }

        // Build page list from OCR output (empty list = no text available).
        IReadOnlyList<string> pages = [];
        if (!string.IsNullOrEmpty(document.ExtractedText))
        {
            var ocrPages = JsonSerializer.Deserialize<IReadOnlyList<OcrPageResult>>(document.ExtractedText);
            pages = ocrPages?.Select(p => p.Text).ToList() ?? [];
        }

        try
        {
            if (pages.Count == 0 || pages.All(p => string.IsNullOrWhiteSpace(p)))
            {
                // No text to process — store empty entities array, mark Processed.
                _logger.LogInformation(
                    "NER skipped (no extracted text) for document {DocumentId}. Marking Processed.",
                    command.DocumentId);
                document.Entities       = "[]";
                document.ProcessingStatus = DocumentProcessingStatus.Processed;
                await _uow.SaveChangesAsync(ct);
                return;
            }

            var entities = await _nerClient.ExtractAsync(
                pages, _settings.ConfidenceThreshold, ct);

            document.Entities         = JsonSerializer.Serialize(entities);
            document.ProcessingStatus = DocumentProcessingStatus.Processed;

            _logger.LogInformation(
                "NER completed for document {DocumentId}. Entities={Count}.",
                command.DocumentId, entities.Count);
        }
        catch (NerModelUnavailableException ex)
        {
            // Re-throw so Hangfire retries the job; status stays Processing.
            _logger.LogWarning(ex, "NER model unavailable for document {DocumentId}. Job will be retried.",
                command.DocumentId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NER extraction failed for document {DocumentId}.", command.DocumentId);
            document.ProcessingStatus = DocumentProcessingStatus.Failed;
        }

        await _uow.SaveChangesAsync(ct);
    }
}
```

---

### 6. Create `INerJobScheduler`

Create `src/HealthPlatform.Application/Interfaces/INerJobScheduler.cs`:

```csharp
namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Enqueues background NER jobs for clinical documents.
/// Implementations live in the Infrastructure layer.
/// </summary>
public interface INerJobScheduler
{
    /// <summary>
    /// Enqueues a fire-and-forget NER job for <paramref name="documentId"/>.
    /// </summary>
    void Enqueue(Guid documentId);
}
```

---

### 7. Create `DocumentNerJob`

Create `src/HealthPlatform.Infrastructure/Jobs/DocumentNerJob.cs`:

```csharp
using HealthPlatform.Application.Features.Documents;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Infrastructure.Jobs;

/// <summary>
/// Hangfire fire-and-forget job dispatching <see cref="ProcessDocumentNerCommand"/>
/// for a single document after successful OCR.
/// </summary>
public sealed class DocumentNerJob
{
    private readonly IServiceScopeFactory    _scopeFactory;
    private readonly ILogger<DocumentNerJob> _logger;

    public DocumentNerJob(
        IServiceScopeFactory    scopeFactory,
        ILogger<DocumentNerJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    /// <summary>Entry point invoked by Hangfire.</summary>
    public async Task ExecuteAsync(Guid documentId, CancellationToken ct = default)
    {
        _logger.LogInformation("DocumentNerJob started for document {DocumentId}.", documentId);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var sender            = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new ProcessDocumentNerCommand(documentId), ct);

        _logger.LogInformation("DocumentNerJob completed for document {DocumentId}.", documentId);
    }
}
```

---

### 8. Create `HangfireNerJobScheduler`

Create `src/HealthPlatform.Infrastructure/Documents/HangfireNerJobScheduler.cs`:

```csharp
using Hangfire;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Infrastructure.Jobs;

namespace HealthPlatform.Infrastructure.Documents;

/// <summary>
/// Enqueues <see cref="DocumentNerJob"/> via Hangfire fire-and-forget.
/// </summary>
internal sealed class HangfireNerJobScheduler : INerJobScheduler
{
    private readonly IBackgroundJobClient _jobs;

    public HangfireNerJobScheduler(IBackgroundJobClient jobs)
    {
        _jobs = jobs;
    }

    public void Enqueue(Guid documentId)
    {
        _jobs.Enqueue<DocumentNerJob>(
            job => job.ExecuteAsync(documentId, CancellationToken.None));
    }
}
```

---

### 9. Create `AiServiceNerClient`

Create `src/HealthPlatform.Infrastructure/Documents/AiServiceNerClient.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HealthPlatform.Application.Features.Documents;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Application.Settings;
using HealthPlatform.Domain.Common.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthPlatform.Infrastructure.Documents;

/// <summary>
/// Calls the Python AI service <c>POST /extraction/ner</c> endpoint.
/// </summary>
internal sealed class AiServiceNerClient : INerClient
{
    private readonly HttpClient                    _http;
    private readonly NerSettings                   _settings;
    private readonly ILogger<AiServiceNerClient>   _logger;

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public AiServiceNerClient(
        HttpClient                  http,
        IOptions<NerSettings>       settings,
        ILogger<AiServiceNerClient> logger)
    {
        _http     = http;
        _settings = settings.Value;
        _logger   = logger;
    }

    public async Task<IReadOnlyList<NerEntity>> ExtractAsync(
        IReadOnlyList<string> pages,
        double confidenceThreshold,
        CancellationToken ct)
    {
        var requestBody = new
        {
            pages                = pages,
            confidence_threshold = confidenceThreshold,
        };

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync("/extraction/ner", requestBody, _json, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request to AI service NER endpoint failed.");
            throw new NerModelUnavailableException("AI service is unreachable.", ex);
        }

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            _logger.LogWarning("AI service returned 503 — NER model unavailable.");
            throw new NerModelUnavailableException();
        }

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<NerApiResponse>(_json, ct)
            ?? throw new InvalidOperationException("AI service returned null NER response.");

        return result.Entities
            .Select(e => new NerEntity(
                e.Text,
                e.Type,
                e.StartOffset,
                e.EndOffset,
                e.ConfidenceScore,
                e.LowConfidence))
            .ToList();
    }

    // ── Private response DTOs (match AI service JSON shape) ──────────────

    private sealed record NerApiResponse(
        [property: JsonPropertyName("entities")] List<NerEntityDto> Entities
    );

    private sealed record NerEntityDto(
        [property: JsonPropertyName("text")]             string Text,
        [property: JsonPropertyName("type")]             string Type,
        [property: JsonPropertyName("start_offset")]     int    StartOffset,
        [property: JsonPropertyName("end_offset")]       int    EndOffset,
        [property: JsonPropertyName("confidence_score")] double ConfidenceScore,
        [property: JsonPropertyName("low_confidence")]   bool   LowConfidence
    );
}
```

---

### 10. Update `ProcessDocumentOcrCommandHandler` — Chain NER

Edit `src/HealthPlatform.Application/Features/Documents/ProcessDocumentOcrCommandHandler.cs`.

**Add constructor parameter** for `INerJobScheduler`:

```csharp
private readonly IUnitOfWork                               _uow;
private readonly IDocumentStorageService                   _storage;
private readonly IOcrService                               _ocr;
private readonly TesseractSettings                         _settings;
private readonly INerJobScheduler                          _nerScheduler;
private readonly ILogger<ProcessDocumentOcrCommandHandler> _logger;

public ProcessDocumentOcrCommandHandler(
    IUnitOfWork                               uow,
    IDocumentStorageService                   storage,
    IOcrService                               ocr,
    IOptions<TesseractSettings>               settings,
    INerJobScheduler                          nerScheduler,
    ILogger<ProcessDocumentOcrCommandHandler> logger)
{
    _uow          = uow;
    _storage      = storage;
    _ocr          = ocr;
    _settings     = settings.Value;
    _nerScheduler = nerScheduler;
    _logger       = logger;
}
```

**Change the success path** — keep status as `Processing` (NER continues the pipeline)
and enqueue the NER job. Replace:

```csharp
// BEFORE (US-046):
document.ExtractedText      = JsonSerializer.Serialize(pages);
document.OcrConfidenceScore = avgConfidence;
document.ProcessingStatus   = DocumentProcessingStatus.Processed;

_logger.LogInformation(
    "OCR completed for document {DocumentId}. Pages={PageCount}, Confidence={Score:F1}%.",
    command.DocumentId, pages.Count, avgConfidence);
```

With:

```csharp
// AFTER (US-047 — status stays Processing; NER job completes the pipeline):
document.ExtractedText      = JsonSerializer.Serialize(pages);
document.OcrConfidenceScore = avgConfidence;
// ProcessingStatus intentionally stays Processing — NER job sets Processed.

_logger.LogInformation(
    "OCR completed for document {DocumentId}. Pages={PageCount}, Confidence={Score:F1}%. Enqueueing NER.",
    command.DocumentId, pages.Count, avgConfidence);
```

And after `await _uow.SaveChangesAsync(ct);` at the end of the try block:

```csharp
await _uow.SaveChangesAsync(ct);
_nerScheduler.Enqueue(document.Id);
```

> **Full handler flow summary after this change:**
> - Start: status → Processing (already set at top of handler)
> - OCR below threshold: status → Failed, save, return (no NER)
> - OCR success: ExtractedText + OcrConfidenceScore stored, status stays Processing, save, NER enqueued
> - OCR exception: status → Failed, save, return (no NER)

---

### 11. Update `DocumentOcrResultDto` — Add Entities

Edit `src/HealthPlatform.Application/Features/Documents/DocumentOcrResultDto.cs`:

```csharp
namespace HealthPlatform.Application.Features.Documents;

public sealed record DocumentOcrResultDto(
    Guid DocumentId,
    string FileName,
    string ProcessingStatus,
    double? OcrConfidenceScore,
    IReadOnlyList<OcrPageResult> Pages,
    IReadOnlyList<NerEntity> Entities
);
```

---

### 12. Update `GetDocumentOcrResultQueryHandler` — Include Entities

Edit `src/HealthPlatform.Application/Features/Documents/GetDocumentOcrResultQueryHandler.cs`.

In the `Handle` method, deserialise entities after deserialising pages, then pass to the DTO:

```csharp
// After deserialising pages:
IReadOnlyList<NerEntity> entities = [];
if (!string.IsNullOrEmpty(document.Entities))
{
    entities = JsonSerializer.Deserialize<IReadOnlyList<NerEntity>>(
        document.Entities) ?? [];
}

return new DocumentOcrResultDto(
    document.Id,
    document.FileName,
    document.ProcessingStatus.ToString(),
    document.OcrConfidenceScore,
    pages,
    entities);
```

---

### 13. Register Services in `DependencyInjection.cs`

Edit `src/HealthPlatform.Infrastructure/DependencyInjection.cs`.

Add the following after the existing `IOcrJobScheduler` registration:

```csharp
// NER — AI service HTTP client + job scheduler
services.Configure<NerSettings>(configuration.GetSection(NerSettings.SectionName));

services.AddHttpClient<INerClient, AiServiceNerClient>((sp, client) =>
{
    var settings = sp.GetRequiredService<IOptions<NerSettings>>().Value;
    client.BaseAddress = new Uri(settings.AiServiceBaseUrl);
    client.Timeout     = TimeSpan.FromSeconds(settings.TimeoutSeconds);
    client.DefaultRequestHeaders.Add("X-Internal-Api-Key", settings.InternalApiKey);
});

services.AddTransient<DocumentNerJob>();
services.AddScoped<INerJobScheduler, HangfireNerJobScheduler>();
```

Also add the required using at the top of `DependencyInjection.cs`:

```csharp
using Microsoft.Extensions.Options;
```

> `AddHttpClient<INerClient, AiServiceNerClient>` registers `AiServiceNerClient`
> as a typed `HttpClient` — the `IHttpClientFactory` manages connection pooling
> (avoids socket exhaustion).

---

### 14. Update `appsettings.json`

Edit `src/HealthPlatform.Api/appsettings.json`.

Add the `Ner` section (after the existing `Tesseract` block):

```json
"Ner": {
  "AiServiceBaseUrl": "http://ai:8000",
  "InternalApiKey": "",
  "ConfidenceThreshold": 0.7,
  "TimeoutSeconds": 60
}
```

> `InternalApiKey` is intentionally empty here — it must be set via the
> `INTERNAL_API_KEY` environment variable in `.env.docker` / Docker Compose.
> Never commit real keys to source control (OWASP A02).

---

### 15. Update `appsettings.Development.json`

Edit `src/HealthPlatform.Api/appsettings.Development.json`.

Add the `Ner` section:

```json
"Ner": {
  "AiServiceBaseUrl": "http://localhost:8000",
  "InternalApiKey": "dev-internal-key",
  "ConfidenceThreshold": 0.7,
  "TimeoutSeconds": 60
}
```

---

### 16. Update Angular `document.models.ts`

Edit `src/health-platform-ui/src/app/core/models/document.models.ts`.

Add `NerEntity` interface and extend `DocumentOcrResultDto`:

```typescript
export interface NerEntity {
  text: string;
  type: string;
  startOffset: number;
  endOffset: number;
  confidenceScore: number;
  lowConfidence: boolean;
}
```

In `DocumentOcrResultDto`, add the `entities` field:

```typescript
export interface DocumentOcrResultDto {
  documentId: string;
  fileName: string;
  processingStatus: DocumentProcessingStatus;
  ocrConfidenceScore: number | null;
  pages: OcrPageResult[];
  entities: NerEntity[];
}
```

---

### 17. Update Angular `DocumentDetailComponent` — Show Entities

Edit `src/health-platform-ui/src/app/features/clinical/documents/document-detail.component.ts`.

**Add `TooltipModule` to imports**:

```typescript
import { TooltipModule } from 'primeng/tooltip';
```

Add `TooltipModule` to the component `imports` array.

**Inject a computed getter for entity grouping**:

```typescript
// Computed: group entities by type for display
readonly entityGroups = signal<Record<string, NerEntity[]>>({});
```

Update `load()` — after `this.document.set(result)`:

```typescript
next: (result) => {
  this.document.set(result);
  this.entityGroups.set(this.groupByType(result.entities));
  this.loading.set(false);
},
```

Add the helper method:

```typescript
private groupByType(entities: NerEntity[]): Record<string, NerEntity[]> {
  return entities.reduce(
    (acc, e) => {
      (acc[e.type] ??= []).push(e);
      return acc;
    },
    {} as Record<string, NerEntity[]>,
  );
}
```

**Update the template** — add an entities section after the pages accordion/panel section,
before the closing `</div>` of the document card:

```html
<!-- Named Entities -->
@if (document()!.entities.length > 0) {
  <div class="mt-4">
    <h2 class="text-lg font-semibold mb-2">Extracted Clinical Entities</h2>
    @for (entry of objectEntries(entityGroups()); track entry[0]) {
      <div class="mb-3">
        <span class="text-sm font-semibold text-color-secondary uppercase tracking-wide">
          {{ entry[0] }}
        </span>
        <div class="flex flex-wrap gap-2 mt-1">
          @for (entity of entry[1]; track entity.startOffset) {
            <span
              class="inline-flex align-items-center gap-1 border-round px-2 py-1 text-sm"
              [class.surface-100]="!entity.lowConfidence"
              [class.surface-200]="entity.lowConfidence"
              [pTooltip]="'Confidence: ' + (entity.confidenceScore | percent: '1.0-0') + (entity.lowConfidence ? ' (low)' : '')"
              tooltipPosition="top"
            >
              {{ entity.text }}
              @if (entity.lowConfidence) {
                <i class="pi pi-exclamation-triangle text-yellow-500" style="font-size: 0.7rem"></i>
              }
            </span>
          }
        </div>
      </div>
    }
  </div>
}
```

Add a class helper for `Object.entries`:

```typescript
readonly objectEntries = Object.entries;
```

Add the `PercentPipe` to the component imports array:

```typescript
import { DecimalPipe, PercentPipe } from '@angular/common';
```

---

## File Checklist

| File                                                                                         | Action |
|----------------------------------------------------------------------------------------------|--------|
| `src/HealthPlatform.Application/Interfaces/INerClient.cs`                                    | Create |
| `src/HealthPlatform.Domain/Common/Exceptions/NerModelUnavailableException.cs`                | Create |
| `src/HealthPlatform.Application/Settings/NerSettings.cs`                                     | Create |
| `src/HealthPlatform.Application/Features/Documents/ProcessDocumentNerCommand.cs`             | Create |
| `src/HealthPlatform.Application/Features/Documents/ProcessDocumentNerCommandHandler.cs`      | Create |
| `src/HealthPlatform.Application/Interfaces/INerJobScheduler.cs`                              | Create |
| `src/HealthPlatform.Infrastructure/Jobs/DocumentNerJob.cs`                                   | Create |
| `src/HealthPlatform.Infrastructure/Documents/HangfireNerJobScheduler.cs`                     | Create |
| `src/HealthPlatform.Infrastructure/Documents/AiServiceNerClient.cs`                          | Create |
| `src/HealthPlatform.Application/Features/Documents/ProcessDocumentOcrCommandHandler.cs`      | Modify |
| `src/HealthPlatform.Application/Features/Documents/DocumentOcrResultDto.cs`                  | Modify |
| `src/HealthPlatform.Application/Features/Documents/GetDocumentOcrResultQueryHandler.cs`      | Modify |
| `src/HealthPlatform.Infrastructure/DependencyInjection.cs`                                   | Modify |
| `src/HealthPlatform.Api/appsettings.json`                                                    | Modify |
| `src/HealthPlatform.Api/appsettings.Development.json`                                        | Modify |
| `src/health-platform-ui/src/app/core/models/document.models.ts`                              | Modify |
| `src/health-platform-ui/src/app/features/clinical/documents/document-detail.component.ts`    | Modify |

## Verification

```bash
# .NET build — 0 errors expected
dotnet build src/HealthPlatform.sln --configuration Release

# Angular lint
cd src/health-platform-ui && npx ng lint

# End-to-end pipeline smoke test (docker-compose up required):
# 1. Upload a PDF via POST /api/patients/{id}/documents
# 2. Wait ~5–15 seconds for OCR + NER jobs to complete
# 3. GET /api/patients/{id}/documents/{documentId} → check entities array
# 4. Angular document detail page → entities panel visible
```

## Architecture Notes

- `AiServiceNerClient` uses `IHttpClientFactory` (via `AddHttpClient<TClient, TImpl>`) to avoid socket exhaustion
- `NerModelUnavailableException` re-thrown from `ProcessDocumentNerCommandHandler` — Hangfire retries automatically (default: 10 attempts with exponential back-off)
- `INerJobScheduler` keeps Hangfire out of the Application layer (same pattern as `IOcrJobScheduler`)
- `InternalApiKey` for the AI service must be set via environment variable — never committed to source (OWASP A02)
- OCR status no longer transitions to `Processed` directly; it stays `Processing` until NER completes — the UI shows "Processing" while both jobs run
