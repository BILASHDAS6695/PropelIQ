import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { ActivatedRoute } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';
import { AuthStore } from '../../../core/auth/auth.store';
import type { DocumentOcrResultDto } from '../../../core/models/document.models';
import { DocumentService } from '../../../core/services/document.service';
import { DocumentViewerComponent } from './document-viewer.component';
import { environment } from '../../../../environments/environment';

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
    ButtonModule,
    CardModule,
    DocumentViewerComponent,
    SkeletonModule,
    TagModule,
    RouterLink,
  ],
  template: `
    <div style="max-width: 900px; margin: 0 auto" class="p-3">
      <!-- Back navigation -->
      <p-button
        icon="pi pi-arrow-left"
        label="Back to Documents"
        [text]="true"
        [routerLink]="['..']"
        styleClass="mb-3"
      />

      <!-- Loading skeleton -->
      @if (loading()) {
        <div class="surface-card border-round p-4">
          <p-skeleton height="2rem" styleClass="mb-3" />
          <p-skeleton height="1rem" width="60%" styleClass="mb-4" />
          <p-skeleton height="6rem" />
        </div>
      }

      <!-- Error state -->
      @else if (error()) {
        <div
          class="surface-100 border-round p-4 text-center text-color-secondary"
          style="border-left: 4px solid var(--red-500)"
        >
          <i
            class="pi pi-exclamation-triangle mb-2"
            style="font-size: 1.5rem; color: var(--red-500)"
          ></i>
          <p>{{ error() }}</p>
          <p-button
            label="Retry"
            icon="pi pi-refresh"
            [text]="true"
            (onClick)="load()"
            styleClass="mt-1"
          />
        </div>
      }

      <!-- Document content -->
      @else if (document()) {
        <div class="surface-card border-round p-4">
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
                  Confidence: {{ formatConfidence(document()!.ocrConfidenceScore) }}
                </span>
              }
              @if (
                document()!.processingStatus === 'Processed' &&
                document()!.pages.length > 0 &&
                (isPdf() || isImage())
              ) {
                <p-button
                  [label]="viewMode() === 'split' ? 'Text Only' : 'Side-by-Side'"
                  [icon]="viewMode() === 'split' ? 'pi pi-align-left' : 'pi pi-table'"
                  [text]="true"
                  size="small"
                  (onClick)="viewMode.set(viewMode() === 'split' ? 'text' : 'split')"
                />
              }
            </div>
          </div>

          <!-- OCR in progress -->
          @if (
            document()!.processingStatus === 'Uploaded' ||
            document()!.processingStatus === 'Processing'
          ) {
            <div class="surface-100 border-round p-4 text-center text-color-secondary mb-3">
              <i class="pi pi-spin pi-spinner mb-2" style="font-size: 1.5rem"></i>
              <p>OCR extraction is in progress. Refresh the page in a few seconds.</p>
              <p-button
                label="Refresh"
                icon="pi pi-refresh"
                [text]="true"
                (onClick)="load()"
                styleClass="mt-1"
              />
            </div>
          }

          <!-- Failed -->
          @else if (document()!.processingStatus === 'Failed') {
            <div
              class="surface-100 border-round p-4 text-center text-color-secondary mb-3"
              style="border-left: 4px solid var(--red-500)"
            >
              <i
                class="pi pi-times-circle mb-2"
                style="font-size: 1.5rem; color: var(--red-500)"
              ></i>
              <p>OCR extraction failed. The document may be illegible or corrupted.</p>
              <p class="text-sm">Please contact your clinical administrator for manual review.</p>
            </div>
          }

          <!-- Document Viewer (pages + entity highlights + summary) -->
          @else if (document()!.pages.length > 0) {
            <!-- Side-by-side view -->
            @if (viewMode() === 'split' && (isPdf() || isImage())) {
              <div class="flex gap-2 mt-3" style="height: 75vh">
                <!-- Left: original document -->
                <div class="flex-1 overflow-auto border-round surface-50 p-2">
                  @if (isPdf() && safeBlobUrl()) {
                    <object
                      [data]="safeBlobUrl()!"
                      type="application/pdf"
                      class="w-full h-full border-none"
                      style="min-height: 600px"
                    >
                      <p class="text-color-secondary text-sm p-3">
                        Your browser cannot display PDF files inline.
                      </p>
                    </object>
                  } @else if (isImage() && blobUrl()) {
                    <img
                      [src]="blobUrl()!"
                      [alt]="document()!.fileName"
                      class="w-full"
                      style="object-fit: contain"
                    />
                  } @else if (blobLoading()) {
                    <div class="text-center text-color-secondary p-4">
                      <i class="pi pi-spin pi-spinner"></i>
                      <p class="mt-2 text-sm">Loading document…</p>
                    </div>
                  }
                </div>

                <!-- Right: entity-highlighted text -->
                <div class="flex-1 overflow-auto border-round surface-50 p-2">
                  <app-document-viewer
                    [pages]="document()!.pages"
                    [entities]="document()!.entities"
                  />
                </div>
              </div>
            }

            <!-- Text-only view (default) -->
            @else {
              <app-document-viewer [pages]="document()!.pages" [entities]="document()!.entities" />
            }
          }

          <!-- Processed but no text -->
          @else {
            <div class="surface-100 border-round p-4 text-center text-color-secondary">
              <p>No text was extracted from this document.</p>
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class DocumentDetailComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly docSvc = inject(DocumentService);
  private readonly auth = inject(AuthStore);
  private readonly http = inject(HttpClient);
  private readonly sanitizer = inject(DomSanitizer);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly document = signal<DocumentOcrResultDto | null>(null);
  readonly viewMode = signal<'split' | 'text'>('text');
  readonly blobUrl = signal<string | null>(null);
  readonly blobLoading = signal(false);

  readonly rawDocumentUrl = computed(() => {
    const doc = this.document();
    const userId = this.auth.userId();
    if (!doc || !userId) return null;
    return `${environment.apiUrl}/patients/${userId}/documents/${doc.documentId}/raw`;
  });

  readonly safeBlobUrl = computed((): SafeResourceUrl | null => {
    const url = this.blobUrl();
    if (!url) return null;
    return this.sanitizer.bypassSecurityTrustResourceUrl(url);
  });

  readonly isPdf = computed(
    () =>
      this.document()?.processingStatus === 'Processed' &&
      (this.document()?.fileName.toLowerCase().endsWith('.pdf') ?? false),
  );

  readonly isImage = computed(() => {
    const fname = this.document()?.fileName.toLowerCase() ?? '';
    return (
      fname.endsWith('.png') ||
      fname.endsWith('.jpg') ||
      fname.endsWith('.jpeg') ||
      fname.endsWith('.tiff') ||
      fname.endsWith('.tif')
    );
  });

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
        this.loadBlobUrl();
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

  formatConfidence(score: number | null): string {
    return score !== null ? score.toFixed(1) + '%' : '';
  }

  ngOnDestroy(): void {
    const url = this.blobUrl();
    if (url) URL.revokeObjectURL(url);
  }

  private loadBlobUrl(): void {
    const url = this.rawDocumentUrl();
    if (!url) return;
    this.blobLoading.set(true);
    this.http.get(url, { responseType: 'blob' }).subscribe({
      next: (blob) => {
        const existing = this.blobUrl();
        if (existing) URL.revokeObjectURL(existing);
        this.blobUrl.set(URL.createObjectURL(blob));
        this.blobLoading.set(false);
      },
      error: () => this.blobLoading.set(false),
    });
  }
}
