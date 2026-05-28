# Task 003: API Controller Endpoint + Angular Document Upload UI

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-045 |
| **Epic** | EP-007 |
| **Layer** | API (ASP.NET) / Angular (Features/Clinical) |
| **Priority** | Critical |
| **Estimated Effort** | 90 minutes |
| **Dependencies** | Task 001 (`IDocumentStorageService`), Task 002 (`UploadDocumentCommand`, `GetPatientDocumentsQuery`, DTOs) |

## Objective

1. **API**: Add `POST /api/patients/{patientId}/documents` (multipart upload) and
   `GET /api/patients/{patientId}/documents` (document list) to
   `PatientsController`, secured with `PatientOwnership` policy.
2. **Angular**: Replace the stub `DocumentsComponent` with a full upload UI
   using PrimeNG `FileUpload`, a document list with status tags, and a
   `DocumentService` wrapping both API endpoints.

## Acceptance Criteria Covered

- AC: Upload endpoint `POST /patients/{id}/documents` accepts file + metadata
- AC: Supported formats: PDF, PNG, JPG, JPEG, TIFF (validated server-side)
- AC: Maximum file size: 10 MB per file
- AC: Unsupported file type → 400 error message
- AC: File exceeds 10 MB → 413 error message
- AC: Upload linked to patient profile (accessible from 360-view)

---

## Implementation Steps

### 1. Update `PatientsController.cs` — Add Upload + List Endpoints

Edit `src/HealthPlatform.Api/Controllers/PatientsController.cs`.

Add the following `using` statements at the top:

```csharp
using HealthPlatform.Application.Features.Documents;
using HealthPlatform.Application.Interfaces;
```

Replace the existing `PatientsController` class with the extended version:

```csharp
/// <summary>
/// Patient management endpoints — quick-create (staff) and document upload (patient/staff).
/// </summary>
[ApiController]
[Route("api/patients")]
public sealed class PatientsController : ControllerBase
{
    private readonly ISender             _sender;
    private readonly ICurrentUserService _currentUser;

    public PatientsController(ISender sender, ICurrentUserService currentUser)
    {
        _sender      = sender;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Quick-creates a patient profile for an unregistered walk-in.
    /// </summary>
    [HttpPost("quick-create")]
    [Authorize(Policy = PolicyNames.Staff)]
    [ProducesResponseType(typeof(QuickCreatePatientResult),  StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails),  StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> QuickCreate(
        [FromBody] QuickCreatePatientRequest request,
        CancellationToken                    ct)
    {
        var result = await _sender.Send(
            new QuickCreatePatientCommand(
                request.FirstName,
                request.LastName,
                request.Dob,
                request.Phone), ct);

        return CreatedAtAction(nameof(QuickCreate), new { id = result.PatientProfileId }, result);
    }

    /// <summary>
    /// Uploads a clinical document (PDF, PNG, JPG, JPEG, TIFF) for the specified patient.
    /// Files are encrypted at rest with AES-256-CBC before being written to disk.
    /// </summary>
    /// <param name="patientId">Target patient profile ID.</param>
    /// <param name="file">The document file (multipart/form-data).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 Created — <see cref="DocumentUploadResultDto"/>.<br/>
    /// 400 Bad Request — unsupported file type or missing file.<br/>
    /// 413 Payload Too Large — file exceeds 10 MB.<br/>
    /// 422 Unprocessable Entity — validation failed (magic-byte mismatch, etc.).
    /// </returns>
    [HttpPost("{patientId:guid}/documents")]
    [Authorize(Policy = PolicyNames.PatientOwnership)]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MultipartBodyLengthLimit = 10_485_760)]
    [ProducesResponseType(typeof(DocumentUploadResultDto),   StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails),            StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails),            StatusCodes.Status413RequestEntityTooLarge)]
    [ProducesResponseType(typeof(ValidationProblemDetails),  StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadDocument(
        Guid              patientId,
        IFormFile         file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ProblemDetails { Title = "No file provided." });

        if (file.Length > 10_485_760)
            return StatusCode(StatusCodes.Status413RequestEntityTooLarge,
                new ProblemDetails { Title = "File too large. Maximum size: 10 MB" });

        await using var stream = file.OpenReadStream();

        var result = await _sender.Send(new UploadDocumentCommand(
            patientId,
            file.FileName,
            file.ContentType,
            file.Length,
            stream), ct);

        return CreatedAtAction(
            nameof(GetDocuments),
            new { patientId },
            result);
    }

    /// <summary>
    /// Returns all clinical documents uploaded by or for the specified patient,
    /// ordered by upload date descending.
    /// </summary>
    /// <param name="patientId">Target patient profile ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK — list of <see cref="DocumentSummaryDto"/>.</returns>
    [HttpGet("{patientId:guid}/documents")]
    [Authorize(Policy = PolicyNames.PatientOwnership)]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDocuments(
        Guid              patientId,
        CancellationToken ct)
    {
        var docs = await _sender.Send(new GetPatientDocumentsQuery(patientId), ct);
        return Ok(docs);
    }
}
```

> **`[RequestFormLimits]`** caps the multipart body at 10 MB at the ASP.NET layer,
> returning 413 before the handler runs. The `IFormFile.Length` guard is a
> belt-and-braces check for when the limit is increased later.

---

### 2. Create Angular Document Models

Create `src/health-platform-ui/src/app/core/models/document.models.ts`:

```typescript
export interface DocumentUploadResultDto {
  documentId: string;
  fileName: string;
  mimeType: string;
  fileSizeBytes: number;
  uploadedAt: string; // ISO-8601
  processingStatus: DocumentProcessingStatus;
}

export interface DocumentSummaryDto {
  documentId: string;
  fileName: string;
  mimeType: string;
  fileSizeBytes: number;
  uploadedAt: string; // ISO-8601
  processingStatus: DocumentProcessingStatus;
}

export type DocumentProcessingStatus =
  | 'Uploaded'
  | 'Processing'
  | 'Processed'
  | 'Verified'
  | 'Failed';
```

---

### 3. Create `DocumentService`

Create `src/health-platform-ui/src/app/core/services/document.service.ts`:

```typescript
import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import type { DocumentSummaryDto, DocumentUploadResultDto } from '../models/document.models';

@Injectable({ providedIn: 'root' })
export class DocumentService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  /**
   * GET /api/patients/{patientId}/documents
   * Returns all documents for the patient, ordered by upload date descending.
   */
  getDocuments(patientId: string): Observable<DocumentSummaryDto[]> {
    return this.http.get<DocumentSummaryDto[]>(
      `${this.base}/patients/${patientId}/documents`,
    );
  }

  /**
   * POST /api/patients/{patientId}/documents
   * Uploads a file as a multipart/form-data request.
   * Returns the created document record.
   */
  uploadDocument(
    patientId: string,
    file: File,
  ): Observable<DocumentUploadResultDto> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<DocumentUploadResultDto>(
      `${this.base}/patients/${patientId}/documents`,
      form,
    );
  }
}
```

---

### 4. Replace `DocumentsComponent`

Replace the stub in
`src/health-platform-ui/src/app/features/clinical/documents/documents.component.ts`
with the full implementation:

```typescript
import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { FileUploadModule, type FileSelectEvent } from 'primeng/fileupload';
import { SkeletonModule } from 'primeng/skeleton';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { AuthStore } from '../../../core/auth/auth.store';
import type { DocumentSummaryDto } from '../../../core/models/document.models';
import { DocumentService } from '../../../core/services/document.service';

type TagSeverity = 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast';

const STATUS_SEVERITY: Record<string, TagSeverity> = {
  Uploaded: 'info',
  Processing: 'warn',
  Processed: 'secondary',
  Verified: 'success',
  Failed: 'danger',
};

const ACCEPTED_TYPES = '.pdf,.png,.jpg,.jpeg,.tiff,.tif';
const MAX_SIZE_BYTES = 10 * 1024 * 1024; // 10 MB

@Component({
  selector: 'app-documents',
  standalone: true,
  imports: [
    CommonModule,
    FileUploadModule,
    TableModule,
    TagModule,
    ButtonModule,
    SkeletonModule,
    ToastModule,
  ],
  providers: [MessageService],
  template: `
    <p-toast />

    <div class="documents-page p-3" style="max-width: 900px; margin: 0 auto">
      <h1 class="text-2xl font-semibold mb-4">My Documents</h1>

      <!-- Upload dropzone -->
      <p-fileupload
        mode="advanced"
        [accept]="acceptedTypes"
        [maxFileSize]="maxSize"
        [multiple]="false"
        [auto]="false"
        chooseLabel="Choose File"
        uploadLabel="Upload"
        cancelLabel="Clear"
        [customUpload]="true"
        (onSelect)="onFileSelect($event)"
        (uploadHandler)="onUpload($event)"
        (onError)="onSizeError()"
        [disabled]="uploading()"
        styleClass="mb-4"
      >
        <ng-template pTemplate="content">
          <div class="flex flex-column align-items-center justify-content-center py-4 text-color-secondary">
            <i class="pi pi-cloud-upload mb-2" style="font-size: 2rem"></i>
            <span>Drag and drop a file here, or click <strong>Choose File</strong></span>
            <small class="mt-1">PDF, PNG, JPG, TIFF · max 10 MB</small>
          </div>
        </ng-template>
      </p-fileupload>

      <!-- Document list -->
      @if (loading()) {
        @for (i of [1, 2, 3]; track i) {
          <div class="surface-100 border-round p-3 mb-2">
            <p-skeleton height="1.5rem" styleClass="mb-1" />
            <p-skeleton height="1rem" width="40%" />
          </div>
        }
      } @else if (documents().length === 0) {
        <div class="text-center py-5 text-color-secondary">
          <i class="pi pi-file mb-3" style="font-size: 2rem; display: block"></i>
          No documents uploaded yet.
        </div>
      } @else {
        <p-table
          [value]="documents()"
          [paginator]="documents().length > 10"
          [rows]="10"
          styleClass="p-datatable-sm"
          aria-label="Clinical documents"
        >
          <ng-template pTemplate="header">
            <tr>
              <th scope="col">File Name</th>
              <th scope="col">Type</th>
              <th scope="col">Size</th>
              <th scope="col">Uploaded</th>
              <th scope="col">Status</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-doc>
            <tr>
              <td>
                <i [class]="iconFor(doc.mimeType)" class="mr-2 text-color-secondary"></i>
                {{ doc.fileName }}
              </td>
              <td class="text-color-secondary text-sm">{{ doc.mimeType }}</td>
              <td class="text-color-secondary text-sm">{{ formatBytes(doc.fileSizeBytes) }}</td>
              <td class="text-color-secondary text-sm">
                {{ doc.uploadedAt | date: 'MMM d, yyyy h:mm a' }}
              </td>
              <td>
                <p-tag
                  [value]="doc.processingStatus"
                  [severity]="statusSeverity(doc.processingStatus)"
                />
              </td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>
  `,
})
export class DocumentsComponent implements OnInit {
  private readonly docSvc = inject(DocumentService);
  private readonly auth = inject(AuthStore);
  private readonly toast = inject(MessageService);

  readonly documents = signal<DocumentSummaryDto[]>([]);
  readonly loading = signal(true);
  readonly uploading = signal(false);

  readonly acceptedTypes = ACCEPTED_TYPES;
  readonly maxSize = MAX_SIZE_BYTES;

  private selectedFile: File | null = null;

  ngOnInit(): void {
    this.loadDocuments();
  }

  private get patientId(): string {
    return this.auth.userId() ?? '';
  }

  onFileSelect(event: FileSelectEvent): void {
    this.selectedFile = event.files[0] ?? null;
  }

  onSizeError(): void {
    this.toast.add({
      severity: 'error',
      summary: 'File too large',
      detail: 'Maximum upload size is 10 MB.',
      life: 5_000,
    });
  }

  onUpload(_event: unknown): void {
    if (!this.selectedFile || !this.patientId) return;

    this.uploading.set(true);
    this.docSvc.uploadDocument(this.patientId, this.selectedFile).subscribe({
      next: (result) => {
        this.uploading.set(false);
        this.selectedFile = null;
        this.toast.add({
          severity: 'success',
          summary: 'Document uploaded',
          detail: `${result.fileName} has been securely uploaded.`,
          life: 5_000,
        });
        this.loadDocuments();
      },
      error: (err) => {
        this.uploading.set(false);
        const detail =
          err?.status === 400
            ? (err?.error?.title ?? 'Unsupported file type.')
            : err?.status === 413
              ? 'File exceeds the 10 MB limit.'
              : 'Upload failed. Please try again.';
        this.toast.add({ severity: 'error', summary: 'Upload failed', detail, life: 6_000 });
      },
    });
  }

  private loadDocuments(): void {
    if (!this.patientId) return;
    this.loading.set(true);
    this.docSvc.getDocuments(this.patientId).subscribe({
      next: (docs) => {
        this.documents.set(docs);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  statusSeverity(status: string): TagSeverity {
    return STATUS_SEVERITY[status] ?? 'secondary';
  }

  iconFor(mimeType: string): string {
    if (mimeType === 'application/pdf') return 'pi pi-file-pdf';
    if (mimeType.startsWith('image/')) return 'pi pi-image';
    return 'pi pi-file';
  }

  formatBytes(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1_048_576) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / 1_048_576).toFixed(1)} MB`;
  }
}
```

> **Note on `patientId`**: The `DocumentsComponent` reads `auth.userId()` from
> the `AuthStore`. This assumes `AuthUser.id` is the patient's profile ID (GUID
> from the `patient_profiles` table). If the `AuthStore` currently stores the
> `User.id` instead, add a `patientProfileId` field to `AuthUser` and populate
> it from the login API response. Alternatively, add a `GET /api/patients/me`
> endpoint that the component calls on init to resolve the profile ID.

---

### 5. Add `FileUploadModule` to Angular Imports (if not present)

Verify `primeng/fileupload` is importable. If not already installed (check
`package.json`), install it — however, PrimeNG 21 ships `p-fileupload` in the
main `primeng` package; no separate install is needed.

---

### 6. Smoke-Test Checklist

After implementation, verify the following manually in Development:

| Scenario | Expected |
|---|---|
| Upload a valid PDF < 10 MB | 201 Created, document appears in list with status `Uploaded` |
| Upload a `.exe` file | 400 "Unsupported format: .exe. Accepted: PDF, PNG, JPG, TIFF" |
| Upload a file > 10 MB | 413 "File too large. Maximum size: 10 MB" |
| Upload a PNG with PDF extension | 422 "magic-byte mismatch" |
| Upload duplicate filename | Both stored; second gets UUID suffix |
| Interrupt upload mid-stream | No DB record, no orphaned file on disk |
| Patient A tries to GET Patient B's documents | 403 Forbidden |
