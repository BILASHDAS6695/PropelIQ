import { DecimalPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ActivatedRoute } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { PanelModule } from 'primeng/panel';
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
  imports: [ButtonModule, CardModule, DecimalPipe, PanelModule, SkeletonModule, TagModule, RouterLink],
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
          <i class="pi pi-exclamation-triangle mb-2" style="font-size: 1.5rem; color: var(--red-500)"></i>
          <p>{{ error() }}</p>
          <p-button label="Retry" icon="pi pi-refresh" [text]="true" (onClick)="load()" styleClass="mt-1" />
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
                  Confidence: {{ document()!.ocrConfidenceScore | number: '1.1-1' }}%
                </span>
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
              <p-button label="Refresh" icon="pi pi-refresh" [text]="true" (onClick)="load()" styleClass="mt-1" />
            </div>
          }

          <!-- Failed -->
          @else if (document()!.processingStatus === 'Failed') {
            <div
              class="surface-100 border-round p-4 text-center text-color-secondary mb-3"
              style="border-left: 4px solid var(--red-500)"
            >
              <i class="pi pi-times-circle mb-2" style="font-size: 1.5rem; color: var(--red-500)"></i>
              <p>OCR extraction failed. The document may be illegible or corrupted.</p>
              <p class="text-sm">Please contact your clinical administrator for manual review.</p>
            </div>
          }

          <!-- Extracted text pages -->
          @else if (document()!.pages.length > 0) {
            @for (page of document()!.pages; track page.pageNumber) {
              <p-panel
                [header]="'Page ' + page.pageNumber + '  (' + (page.confidenceScore | number: '1.1-1') + '% confidence)'"
                [toggleable]="true"
                styleClass="mb-2"
              >
                <pre
                  class="m-0 white-space-pre-wrap"
                  style="font-family: inherit; font-size: 0.9rem; line-height: 1.6"
                >{{ page.text || '(No text detected on this page)' }}</pre>
              </p-panel>
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
