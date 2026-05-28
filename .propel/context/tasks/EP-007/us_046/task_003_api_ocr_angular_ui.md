# Task 003: API OCR Result Endpoint + Angular Document Detail UI

## Context

| Field                | Value                                                                           |
|----------------------|---------------------------------------------------------------------------------|
| **User Story**       | US-046                                                                          |
| **Epic**             | EP-007                                                                          |
| **Layer**            | API (ASP.NET) / Angular (Features/Clinical) / Docker                           |
| **Priority**         | Critical                                                                        |
| **Estimated Effort** | 75 minutes                                                                      |
| **Dependencies**     | Task 001 (`TesseractOcrService`, `OcrPageResult`) and Task 002 (`GetDocumentOcrResultQuery`, `DocumentOcrResultDto`, `ProcessDocumentOcrCommandHandler`) must be complete |

## Objective

1. **API**: Add `GET /api/patients/{patientId}/documents/{documentId}` to
   `PatientsController`, dispatching `GetDocumentOcrResultQuery` and returning
   the full OCR result (`pages`, `ocrConfidenceScore`, `processingStatus`).
2. **Angular**:
   - Extend `document.models.ts` with `OcrPageResult` and `DocumentOcrResultDto`
   - Add `getDocumentOcrResult(patientId, documentId)` to `DocumentService`
   - Create `DocumentDetailComponent` — displays per-page OCR text, status, and
     confidence in a PrimeNG `Panel` / `Accordion` layout
   - Add `documents/:documentId` child route to `CLINICAL_ROUTES`
   - Add "View OCR" action button to `DocumentsComponent` document list rows
3. **Dockerfile**: Install `tesseract-ocr` and English tessdata in
   `src/HealthPlatform.Api/Dockerfile` so the production container can run OCR.

## Acceptance Criteria Covered

- AC: OCR pipeline triggered automatically on document upload (Hangfire — verified through status polling)
- AC: Extracted text accessible per page with confidence score
- AC: Document status: Uploaded → Processing → Processed visible in UI
- AC: Failed OCR logged with error, document status → Failed shown in UI
- AC: Processing time target: <30s (visible via status tag in document list)
- AC: Completely illegible scan → low confidence, status "Failed", shown in UI

---

## Implementation Steps

### 1. Add OCR Result Endpoint to `PatientsController`

Edit `src/HealthPlatform.Api/Controllers/PatientsController.cs`.

Add the following action **inside** the `PatientsController` class, after the `GetDocuments` action:

```csharp
/// <summary>
/// Returns the OCR extraction result for a specific clinical document,
/// including extracted text per page and aggregate confidence score.
/// The <paramref name="patientId"/> route value is the patient's <c>User.Id</c>.
/// </summary>
/// <param name="patientId">Patient's User.Id (matches JWT sub for ownership check).</param>
/// <param name="documentId">The document's unique identifier.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>
/// 200 OK — <see cref="DocumentOcrResultDto"/> with page-level OCR text.<br/>
/// 404 Not Found — document does not exist or does not belong to this patient.
/// </returns>
[HttpGet("{patientId:guid}/documents/{documentId:guid}")]
[Authorize(Policy = PolicyNames.PatientOwnership)]
[ProducesResponseType(typeof(DocumentOcrResultDto), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails),       StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetDocumentOcrResult(
    Guid          patientId,
    Guid          documentId,
    CancellationToken ct)
{
    var result = await _sender.Send(
        new GetDocumentOcrResultQuery(patientId, documentId), ct);
    return Ok(result);
}
```

No new `using` statements are required — `GetDocumentOcrResultQuery` and
`DocumentOcrResultDto` are already in scope via the existing
`using HealthPlatform.Application.Features.Documents;` directive.

---

### 2. Extend Angular `document.models.ts`

Edit `src/health-platform-ui/src/app/core/models/document.models.ts`.

Append to the end of the file:

```typescript
export interface OcrPageResult {
  pageNumber: number;
  text: string;
  confidenceScore: number;
}

export interface DocumentOcrResultDto {
  documentId: string;
  fileName: string;
  processingStatus: DocumentProcessingStatus;
  ocrConfidenceScore: number | null;
  pages: OcrPageResult[];
}
```

---

### 3. Add `getDocumentOcrResult` to `DocumentService`

Edit `src/health-platform-ui/src/app/core/services/document.service.ts`.

Add the import for `DocumentOcrResultDto` to the existing import line:

```typescript
import type {
  DocumentOcrResultDto,
  DocumentSummaryDto,
  DocumentUploadResultDto,
} from '../models/document.models';
```

Add the new method **after** `uploadDocument`:

```typescript
/**
 * GET /api/patients/{patientId}/documents/{documentId}
 * Returns OCR result for a single document.
 * patientId is the patient's User.Id (JWT sub).
 */
getDocumentOcrResult(patientId: string, documentId: string): Observable<DocumentOcrResultDto> {
  return this.http.get<DocumentOcrResultDto>(
    `${this.base}/patients/${patientId}/documents/${documentId}`,
  );
}
```

---

### 4. Create `DocumentDetailComponent`

Create `src/health-platform-ui/src/app/features/clinical/documents/document-detail.component.ts`:

```typescript
import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AccordionModule } from 'primeng/accordion';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';
import { AuthStore } from '../../../core/auth/auth.store';
import type { DocumentOcrResultDto } from '../../../core/models/document.models';
import { DocumentService } from '../../../core/services/document.service';

type TagSeverity = 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast';

const STATUS_SEVERITY: Record<string, TagSeverity> = {
  Uploaded: 'info',
  Processing: 'warn',
  Processed: 'secondary',
  Verified: 'success',
  Failed: 'danger',
};

@Component({
  selector: 'app-document-detail',
  standalone: true,
  imports: [
    CommonModule,
    AccordionModule,
    ButtonModule,
    CardModule,
    SkeletonModule,
    TagModule,
    RouterLink,
  ],
  template: `
    <div class="document-detail p-3" style="max-width: 900px; margin: 0 auto">
      <!-- Back button -->
      <p-button
        icon="pi pi-arrow-left"
        label="Back to Documents"
        [text]="true"
        [routerLink]="['..']"
        styleClass="mb-3"
      />

      @if (loading()) {
        <p-skeleton height="2rem" styleClass="mb-3" />
        <p-skeleton height="1.5rem" width="60%" styleClass="mb-4" />
        @for (i of [1, 2]; track i) {
          <p-skeleton height="4rem" styleClass="mb-2" />
        }
      } @else if (error()) {
        <div class="surface-100 border-round p-4 text-center text-color-secondary">
          <i class="pi pi-exclamation-triangle mb-2" style="font-size: 2rem; color: var(--red-500)"></i>
          <p>{{ error() }}</p>
          <p-button label="Retry" icon="pi pi-refresh" (onClick)="load()" styleClass="mt-2" />
        </div>
      } @else if (document()) {
        <!-- Header -->
        <div class="flex align-items-center justify-content-between mb-3">
          <div>
            <h1 class="text-2xl font-semibold m-0">{{ document()!.fileName }}</h1>
            <span class="text-color-secondary text-sm">{{ document()!.documentId }}</span>
          </div>
          <div class="flex align-items-center gap-2">
            <p-tag
              [value]="document()!.processingStatus"
              [severity]="getSeverity(document()!.processingStatus)"
            />
            @if (document()!.ocrConfidenceScore !== null) {
              <span class="text-sm text-color-secondary">
                Confidence: {{ document()!.ocrConfidenceScore | number: '1.1-1' }}%
              </span>
            }
          </div>
        </div>

        <!-- OCR in progress -->
        @if (document()!.processingStatus === 'Uploaded' || document()!.processingStatus === 'Processing') {
          <div class="surface-100 border-round p-4 text-center text-color-secondary mb-3">
            <i class="pi pi-spin pi-spinner mb-2" style="font-size: 1.5rem"></i>
            <p>OCR extraction is in progress. Refresh the page in a few seconds.</p>
            <p-button label="Refresh" icon="pi pi-refresh" [text]="true" (onClick)="load()" styleClass="mt-1" />
          </div>
        }

        <!-- Failed -->
        @else if (document()!.processingStatus === 'Failed') {
          <div class="surface-100 border-round p-4 text-center text-color-secondary mb-3"
               style="border-left: 4px solid var(--red-500)">
            <i class="pi pi-times-circle mb-2" style="font-size: 1.5rem; color: var(--red-500)"></i>
            <p>OCR extraction failed. The document may be illegible or corrupted.</p>
            <p class="text-sm">Please contact your clinical administrator for manual review.</p>
          </div>
        }

        <!-- Extracted text pages -->
        @else if (document()!.pages.length > 0) {
          <p-accordion [multiple]="true">
            @for (page of document()!.pages; track page.pageNumber) {
              <p-accordionTab
                [header]="'Page ' + page.pageNumber + '  (' + (page.confidenceScore | number: '1.1-1') + '% confidence)'"
              >
                <pre class="m-0 white-space-pre-wrap"
                     style="font-family: inherit; font-size: 0.9rem; line-height: 1.6">{{
                  page.text || '(No text detected on this page)'
                }}</pre>
              </p-accordionTab>
            }
          </p-accordion>
        }

        <!-- No text but processed -->
        @else {
          <div class="surface-100 border-round p-4 text-center text-color-secondary">
            <p>No text was extracted from this document.</p>
          </div>
        }
      }
    </div>
  `,
})
export class DocumentDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly docSvc = inject(DocumentService);
  private readonly auth = inject(AuthStore);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly document = signal<DocumentOcrResultDto | null>(null);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    const patientId = this.auth.userId();
    const documentId = this.route.snapshot.paramMap.get('documentId');

    if (!patientId || !documentId) {
      this.error.set('Invalid route parameters.');
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    this.docSvc.getDocumentOcrResult(patientId, documentId).subscribe({
      next: (result) => {
        this.document.set(result);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load document OCR result. Please try again.');
        this.loading.set(false);
      },
    });
  }

  getSeverity(status: string): TagSeverity {
    return STATUS_SEVERITY[status] ?? 'secondary';
  }
}
```

---

### 5. Update `clinical.routes.ts` — Add Detail Route

Edit `src/health-platform-ui/src/app/features/clinical/clinical.routes.ts`.

Replace the current content with:

```typescript
import { Routes } from '@angular/router';

export const CLINICAL_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./documents/documents.component').then((m) => m.DocumentsComponent),
  },
  {
    path: 'documents/:documentId',
    loadComponent: () =>
      import('./documents/document-detail.component').then((m) => m.DocumentDetailComponent),
  },
];
```

---

### 6. Add "View OCR" Navigation to `DocumentsComponent`

Edit `src/health-platform-ui/src/app/features/clinical/documents/documents.component.ts`.

**Add `RouterLink` import** to the `imports` array:

```typescript
import { RouterLink } from '@angular/router';
```

```typescript
imports: [
  CommonModule,
  FileUploadModule,
  TableModule,
  TagModule,
  ButtonModule,
  SkeletonModule,
  ToastModule,
  RouterLink,    // ← add this
],
```

**Add a "View" button column** to the `p-table` template — append a new column header and cell
inside the existing `<p-table>` block, after the `Processing Status` column:

```html
<!-- In the header row -->
<th style="width: 6rem">Actions</th>

<!-- In the body row (inside @for) -->
<td>
  <p-button
    icon="pi pi-search"
    [text]="true"
    size="small"
    pTooltip="View OCR result"
    [routerLink]="['documents', doc.documentId]"
  />
</td>
```

> **Exact placement**: add the `<th>` after the existing `<th>Processing Status</th>` header,
> and add the `<td>` after the existing status-tag `<td>`.

---

### 7. Update `HealthPlatform.Api/Dockerfile` — Install Tesseract

Edit `src/HealthPlatform.Api/Dockerfile`.

Locate the `FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final` (or equivalent runtime stage)
and add an `apt-get` step **before** the `WORKDIR` or `COPY` instructions:

```dockerfile
# Install Tesseract OCR and English language data for local OCR processing (TR-026/ADR-004)
RUN apt-get update && apt-get install -y --no-install-recommends \
        tesseract-ocr \
        tesseract-ocr-eng \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*
```

> The `tesseract-ocr-eng` package installs `eng.traineddata` to
> `/usr/share/tesseract-ocr/5/tessdata/` on Debian/Ubuntu.
> The `appsettings.json` value `"TessDataPath": "/usr/share/tessdata"` points
> to the symlink `/usr/share/tessdata` → the versioned directory.
> If Tesseract 4.x is installed instead, the path may be
> `/usr/share/tesseract-ocr/4.00/tessdata/` — adjust `TessDataPath` accordingly.

---

## File Checklist

| File | Action |
|------|--------|
| `src/HealthPlatform.Api/Controllers/PatientsController.cs` | Add `GetDocumentOcrResult` action |
| `src/health-platform-ui/src/app/core/models/document.models.ts` | Append `OcrPageResult`, `DocumentOcrResultDto` |
| `src/health-platform-ui/src/app/core/services/document.service.ts` | Add `getDocumentOcrResult` method + import |
| `src/health-platform-ui/src/app/features/clinical/documents/document-detail.component.ts` | Create (new) |
| `src/health-platform-ui/src/app/features/clinical/clinical.routes.ts` | Add `documents/:documentId` route |
| `src/health-platform-ui/src/app/features/clinical/documents/documents.component.ts` | Add `RouterLink` import + "View" column |
| `src/HealthPlatform.Api/Dockerfile` | Add Tesseract `apt-get` install step |

## Definition of Done

- [ ] `GET /api/patients/{patientId}/documents/{documentId}` returns 200 with `DocumentOcrResultDto`
- [ ] `DocumentDetailComponent` renders per-page OCR text in a collapsible accordion
- [ ] Status tags show the correct PrimeNG severity for all 5 statuses (`Uploaded`, `Processing`, `Processed`, `Verified`, `Failed`)
- [ ] "View OCR" button in document list navigates to `/clinical/documents/{documentId}`
- [ ] `ng lint` passes: `npx ng lint` → "All files pass linting"
- [ ] `ng build --configuration production` succeeds
- [ ] Docker image builds without error including Tesseract installation layer
- [ ] End-to-end smoke test: upload a test PDF → status shows `Uploaded` → after ~30s refreshes to `Processed` → "View OCR" shows extracted text
