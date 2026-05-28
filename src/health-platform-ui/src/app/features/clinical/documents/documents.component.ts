import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { FileUploadModule } from 'primeng/fileupload';
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
          <div
            class="flex flex-column align-items-center justify-content-center py-4 text-color-secondary"
          >
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

  onFileSelect(event: { files: File[] }): void {
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

