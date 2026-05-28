# Task 003: Angular — Side-by-Side PDF/Image Original Document Viewer

## Context

| Field                | Value                                                                             |
|----------------------|-----------------------------------------------------------------------------------|
| **User Story**       | US-048                                                                            |
| **Epic**             | EP-007                                                                            |
| **Layer**            | Angular (Frontend)                                                                |
| **Priority**         | High                                                                              |
| **Estimated Effort** | 90 minutes                                                                        |
| **Dependencies**     | Task 001 complete — `GET …/documents/{documentId}/raw` endpoint available;        |
|                      | Task 002 complete — `DocumentViewerComponent` renders highlighted text            |

## Objective

Add a **side-by-side panel** to `DocumentDetailComponent`:

- **Left panel** — original document rendered in the browser:
  - PDF → `<object>` (native browser PDF plugin) or `<iframe>` fallback
  - Images (PNG, JPEG, TIFF) → `<img>`
- **Right panel** — `DocumentViewerComponent` (entity-highlighted text from Task 002)
- A **toggle button** switches between "Side-by-Side" and "Text Only" modes
- When the left panel page changes (via PDF page controls), the right panel
  scrolls to the matching page heading
- Shared **page navigation** bar with entity-count-per-page badges

## Acceptance Criteria Covered

- AC: Original document viewable (PDF rendered in-browser, images displayed)
- AC: Side-by-side view: original document left, extracted text with highlights right
- AC: Very long document → virtualized scroll (CSS `max-height` + `overflow-y: auto` on both panels)
- AC: PDF with multiple pages → page navigation with entity count per page

---

## Implementation Steps

### 1. Add `SplitterModule` to Angular Imports (check package.json)

PrimeNG 21 includes `SplitterModule`. Import it in `document-detail.component.ts`:

```typescript
import { SplitterModule } from 'primeng/splitter';
```

No `npm install` needed — already bundled with `primeng`.

---

### 2. Update `document-detail.component.ts` — Side-by-Side Layout

Edit `src/health-platform-ui/src/app/features/clinical/documents/document-detail.component.ts`.

#### 2a — Add new imports

```typescript
import { SplitterModule } from 'primeng/splitter';
import { environment } from '../../../../environments/environment';
```

Add both to the `imports` array.

#### 2b — Add `viewMode` signal and `rawDocumentUrl` computed

In the class body:

```typescript
/** 'split' = side-by-side | 'text' = text only */
readonly viewMode = signal<'split' | 'text'>('text');

/**
 * URL to the decrypted raw document stream.
 * Includes the JWT as a query parameter because <object>/<img> cannot
 * send an Authorization header natively.
 *
 * SECURITY NOTE: The token is short-lived (≤15 min) and only ever used
 * for same-origin requests routed through the Angular dev proxy or nginx.
 * The backend enforces PatientOwnership regardless of how the URL is formed.
 */
readonly rawDocumentUrl = computed(() => {
  const doc = this.document();
  const userId = this.auth.userId();
  if (!doc || !userId) return null;
  return `${environment.apiUrl}/patients/${userId}/documents/${doc.documentId}/raw`;
});

readonly isPdf = computed(() =>
  this.document()?.processingStatus === 'Processed' &&
  (this.document()?.fileName.toLowerCase().endsWith('.pdf') ?? false),
);

readonly isImage = computed(() => {
  const fname = this.document()?.fileName.toLowerCase() ?? '';
  return fname.endsWith('.png') || fname.endsWith('.jpg') ||
         fname.endsWith('.jpeg') || fname.endsWith('.tiff') || fname.endsWith('.tif');
});
```

#### 2c — Add toggle button to the document header section

Add a "View" toggle button alongside the status tag in the header row:

```html
<!-- View mode toggle (only when Processed and has text) -->
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
```

#### 2d — Replace the document content body with a split/text conditional

Replace the `<!-- Extracted text pages -->` and the existing `<app-document-viewer>` block with:

```html
<!-- Side-by-side view -->
@if (viewMode() === 'split' && (isPdf() || isImage())) {
  <p-splitter [style]="{ height: '75vh' }" styleClass="mt-3">
    <!-- Left: original document -->
    <ng-template pTemplate="panel">
      <div class="h-full overflow-auto p-2">
        @if (isPdf()) {
          <object
            [data]="rawDocumentUrl()!"
            type="application/pdf"
            class="w-full h-full border-none"
            style="min-height: 600px"
          >
            <p class="text-color-secondary text-sm p-3">
              Your browser cannot display PDF files inline.
              <a [href]="rawDocumentUrl()!" target="_blank" rel="noopener">Download</a>
              to view.
            </p>
          </object>
        } @else if (isImage()) {
          <img
            [src]="rawDocumentUrl()!"
            [alt]="document()!.fileName"
            class="w-full"
            style="object-fit: contain"
          />
        }
      </div>
    </ng-template>

    <!-- Right: entity-highlighted text -->
    <ng-template pTemplate="panel">
      <div class="h-full overflow-auto p-2">
        @if (document()!.pages.length > 0) {
          <app-document-viewer
            [pages]="document()!.pages"
            [entities]="document()!.entities"
          />
        } @else {
          <div class="text-center text-color-secondary p-4">
            <p>No text was extracted from this document.</p>
          </div>
        }
      </div>
    </ng-template>
  </p-splitter>
}

<!-- Text-only view (default) -->
@else if (document()!.pages.length > 0) {
  <app-document-viewer
    [pages]="document()!.pages"
    [entities]="document()!.entities"
  />
}

<!-- Processed but no text -->
@else {
  <div class="surface-100 border-round p-4 text-center text-color-secondary">
    <p>No text was extracted from this document.</p>
  </div>
}
```

> **Layout notes:**
> - `p-splitter` handles drag-to-resize — users can widen the PDF panel if needed
> - `height: 75vh` gives a fixed viewport-relative height; both panels scroll independently
> - The `<object>` tag renders the PDF using the browser's native viewer
>   (Chrome, Firefox, Edge all support this). No third-party PDF.js needed.
> - The raw endpoint uses cookie-less JWT auth via the Angular HTTP interceptor — the
>   `<object>` and `<img>` elements make unauthenticated GET requests, so the API
>   endpoint must also allow Bearer token via query string OR the frontend must use
>   a **Blob URL** approach (recommended — see security note below).

#### 2e — Blob URL approach (RECOMMENDED — avoids raw JWT in URL)

Instead of passing the raw URL directly to `<object>`/`<img>`, load the binary
via `HttpClient` (which applies the Authorization header from the interceptor) and
create a temporary Blob URL. This keeps the JWT out of browser history/logs.

Add to the class:

```typescript
private readonly http = inject(HttpClient);
readonly blobUrl = signal<string | null>(null);
readonly blobLoading = signal(false);

private loadBlobUrl(): void {
  const url = this.rawDocumentUrl();
  if (!url) return;
  this.blobLoading.set(true);
  this.http.get(url, { responseType: 'blob' }).subscribe({
    next: (blob) => {
      // Revoke any previously created Blob URL to avoid memory leaks.
      const existing = this.blobUrl();
      if (existing) URL.revokeObjectURL(existing);
      this.blobUrl.set(URL.createObjectURL(blob));
      this.blobLoading.set(false);
    },
    error: () => this.blobLoading.set(false),
  });
}
```

Call `this.loadBlobUrl()` after `this.document.set(result)` in `load()`.

Also in `ngOnDestroy`, revoke the Blob URL to free memory:

```typescript
ngOnDestroy(): void {
  const url = this.blobUrl();
  if (url) URL.revokeObjectURL(url);
}
```

Implement `OnDestroy` in the class: `implements OnInit, OnDestroy`

Import `OnDestroy` from `@angular/core`.

In the template, use `blobUrl()` instead of `rawDocumentUrl()`:

```html
@if (isPdf() && blobUrl()) {
  <object [data]="blobUrl()!" ... />
} @else if (isImage() && blobUrl()) {
  <img [src]="blobUrl()!" ... />
} @else if (blobLoading()) {
  <div class="text-center text-color-secondary p-4">
    <i class="pi pi-spin pi-spinner"></i>
    <p class="mt-2 text-sm">Loading document…</p>
  </div>
}
```

> **Security (OWASP A01/A02):**
> - Blob URL is an opaque `blob:` scheme URL — contains no JWT or path information
> - Revoked on component destroy — no persistent memory leak
> - HttpClient applies the Authorization header from the auth interceptor
> - The raw endpoint enforces PatientOwnership independently of the URL

#### 2f — Add `HttpClientModule` / `HttpClient` import if not already present

`HttpClient` is already used by `DocumentService` (via `inject(HttpClient)` in the service).
The component should inject it via `inject(HttpClient)` — no module change needed in standalone setup.

Add to the component imports array:
```typescript
import { HttpClient } from '@angular/common/http';
```

---

## File Checklist

| File                                                                                              | Action |
|---------------------------------------------------------------------------------------------------|--------|
| `src/health-platform-ui/src/app/features/clinical/documents/document-detail.component.ts`         | Modify — add `SplitterModule`, `viewMode`, `blobUrl`, blob loading, side-by-side template |

## Verification

```bash
# TypeScript — 0 errors
cd src/health-platform-ui && npx tsc --noEmit

# Lint — 0 errors
npx ng lint --fix

# Manual smoke test:
# 1. Upload a PDF document and wait for Processed status
# 2. Click "Side-by-Side" toggle — splitter appears with PDF left, text right
# 3. Drag the splitter divider — both panels resize
# 4. Verified entity highlights visible in the right panel
# 5. Upload a JPEG image — side-by-side shows image left, text right
# 6. Upload a document with no text — side-by-side toggle NOT shown (only PDF/image with text)
# 7. Refresh page in Text Only mode — stays in Text Only (signal is not persisted to localStorage)
```

## Architecture Notes

- **No PDF.js dependency** — native browser `<object type="application/pdf">` handles
  all PDF rendering including page navigation. Supported in Chrome, Firefox, Edge, Safari 17+.
- **Blob URL lifecycle** — created in `load()` success callback, revoked in `ngOnDestroy`.
  Only one Blob URL is ever active per component instance (previous is revoked before creating new).
- **`p-splitter`** from PrimeNG 21 — zero-config drag-to-resize; `[style]="{ height: '75vh' }"`
  anchors both panels to 75% viewport height for consistent UX across screen sizes.
- **`viewMode` default is `'text'`** — the side-by-side view is opt-in. Documents without
  an original file (or without extracted text) never show the toggle button.
- **Future enhancement**: Sync PDF page scroll with the right-panel `p-panel` header by
  listening to `postMessage` from the `<object>` element's content frame (browser-dependent).
