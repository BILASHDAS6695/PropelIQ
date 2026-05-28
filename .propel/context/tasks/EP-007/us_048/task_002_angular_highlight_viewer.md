# Task 002: Angular — Entity Highlighting Service + Document Viewer Component

## Context

| Field                | Value                                                                             |
|----------------------|-----------------------------------------------------------------------------------|
| **User Story**       | US-048                                                                            |
| **Epic**             | EP-007                                                                            |
| **Layer**            | Angular (Frontend)                                                                |
| **Priority**         | Critical                                                                          |
| **Estimated Effort** | 90 minutes                                                                        |
| **Dependencies**     | Task 001 complete — `pageNumber` on `NerEntity`; `NerEntity.pageNumber` in Angular model |

## Objective

Replace the plain `<pre>` text display in `DocumentDetailComponent` with a rich
inline entity highlighting viewer. The viewer renders extracted text as a sequence
of `<span>` elements, colour-coding recognised clinical entities by type with
solid/dashed underlines for confidence, per-type toggle checkboxes, click tooltips,
keyboard Next/Previous navigation, and an entity summary panel with counts.

## Acceptance Criteria Covered

- AC: Document viewer displays extracted text with entity highlights (color-coded by type)
- AC: Entity legend: DIAGNOSIS (red), MEDICATION (blue), PROCEDURE (green), LAB (purple), SYMPTOM (orange)
- AC: Click entity → tooltip with entity type + confidence score
- AC: Toggle highlights on/off per entity type
- AC: Entity summary panel: list of all detected entities grouped by type
- AC: Low-confidence entities shown with dashed underline (vs solid for high confidence)
- AC: Keyboard navigation between entities (Next/Previous)
- AC: Document with no entities → "No entities detected" message

---

## Implementation Steps

### 1. Update Angular `NerEntity` Interface

Edit `src/health-platform-ui/src/app/core/models/document.models.ts`.

Add `pageNumber` to `NerEntity`:

```typescript
export interface NerEntity {
  text: string;
  type: string;
  startOffset: number;
  endOffset: number;
  confidenceScore: number;
  lowConfidence: boolean;
  pageNumber: number; // 1-based; 0 = unknown page (pre-US-048 data)
}
```

---

### 2. Create `EntityHighlighterService`

Create `src/health-platform-ui/src/app/core/services/entity-highlighter.service.ts`:

```typescript
import { Injectable } from '@angular/core';
import type { NerEntity } from '../models/document.models';

/** A plain text segment with no entity annotation. */
export interface PlainSegment {
  kind: 'plain';
  text: string;
}

/** A text segment that corresponds to a recognised entity. */
export interface EntitySegment {
  kind: 'entity';
  text: string;
  entity: NerEntity;
  /** Sequential index across all visible entities on the page — used for keyboard nav. */
  index: number;
}

export type TextSegment = PlainSegment | EntitySegment;

/**
 * Splits a page's plain text into an ordered list of `TextSegment` objects
 * so the template can render entity highlights inline without regex substitution
 * in the DOM (XSS-safe: only `textContent` is ever set, never `innerHTML`).
 */
@Injectable({ providedIn: 'root' })
export class EntityHighlighterService {
  /**
   * Build segments for one OCR page.
   *
   * @param pageText - Raw OCR text for the page.
   * @param entities - All entities whose `pageNumber` matches this page, already
   *                   filtered to only the types the user has enabled.
   * @param entityOffset - Running count of visible entities preceding this page,
   *                        used to assign globally-unique sequential indices.
   * @returns Ordered array of plain + entity segments.
   */
  buildSegments(
    pageText: string,
    entities: NerEntity[],
    entityOffset: number = 0,
  ): TextSegment[] {
    if (!entities.length) {
      return [{ kind: 'plain', text: pageText }];
    }

    // Sort by start offset; resolve overlaps by keeping the first entity.
    const sorted = [...entities].sort((a, b) => a.startOffset - b.startOffset);
    const deduped = this.removeOverlaps(sorted);

    const segments: TextSegment[] = [];
    let cursor = 0;
    let entityCounter = entityOffset;

    for (const ent of deduped) {
      const start = Math.max(0, ent.startOffset);
      const end = Math.min(pageText.length, ent.endOffset);

      if (start > cursor) {
        segments.push({ kind: 'plain', text: pageText.slice(cursor, start) });
      }

      if (end > start) {
        segments.push({
          kind: 'entity',
          text: pageText.slice(start, end),
          entity: ent,
          index: entityCounter++,
        });
      }

      cursor = end;
    }

    if (cursor < pageText.length) {
      segments.push({ kind: 'plain', text: pageText.slice(cursor) });
    }

    return segments;
  }

  private removeOverlaps(sorted: NerEntity[]): NerEntity[] {
    const result: NerEntity[] = [];
    let lastEnd = -1;
    for (const ent of sorted) {
      if (ent.startOffset >= lastEnd) {
        result.push(ent);
        lastEnd = ent.endOffset;
      }
    }
    return result;
  }
}
```

---

### 3. Create `DocumentViewerComponent`

Create `src/health-platform-ui/src/app/features/clinical/documents/document-viewer.component.ts`:

```typescript
import { DecimalPipe, KeyValuePipe, PercentPipe } from '@angular/common';
import {
  Component,
  computed,
  ElementRef,
  HostListener,
  inject,
  input,
  signal,
  viewChildren,
} from '@angular/core';
import { CheckboxModule } from 'primeng/checkbox';
import { DividerModule } from 'primeng/divider';
import { PanelModule } from 'primeng/panel';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import {
  EntityHighlighterService,
  type EntitySegment,
  type TextSegment,
} from '../../../core/services/entity-highlighter.service';
import type { NerEntity, OcrPageResult } from '../../../core/models/document.models';

// ── Entity type colour map ────────────────────────────────────────────────────
export const ENTITY_COLORS: Record<string, string> = {
  DIAGNOSIS:  'var(--red-400)',
  MEDICATION: 'var(--blue-400)',
  PROCEDURE:  'var(--green-400)',
  LAB_TEST:   'var(--purple-400)',
  LAB_VALUE:  'var(--purple-300)',
  ANATOMY:    'var(--teal-400)',
  SYMPTOM:    'var(--orange-400)',
};

@Component({
  selector: 'app-document-viewer',
  standalone: true,
  imports: [
    CheckboxModule,
    DecimalPipe,
    DividerModule,
    KeyValuePipe,
    PanelModule,
    PercentPipe,
    TagModule,
    TooltipModule,
  ],
  template: `
    <!-- Entity type toggle legend -->
    @if (allEntityTypes().length > 0) {
      <div class="flex flex-wrap gap-3 mb-3 align-items-center">
        <span class="text-sm font-semibold text-color-secondary">Highlights:</span>
        @for (type of allEntityTypes(); track type) {
          <label class="flex align-items-center gap-1 cursor-pointer text-sm">
            <p-checkbox
              [binary]="true"
              [(ngModel)]="typeEnabled()[type]"
              (onChange)="onTypeToggle(type, $event.checked)"
            />
            <span
              class="px-1 border-round"
              [style.background]="typeColor(type) + '33'"
              [style.border-bottom]="'2px solid ' + typeColor(type)"
            >
              {{ type }}
            </span>
            <span class="text-color-secondary">({{ entityCountByType()[type] ?? 0 }})</span>
          </label>
        }
        <span class="text-color-secondary text-sm ml-auto">
          Entity {{ focusedIndex() + 1 }} / {{ totalVisible() }}
          &nbsp;
          <button
            type="button"
            class="p-link text-color-secondary"
            (click)="prevEntity()"
            [attr.aria-label]="'Previous entity'"
            title="Previous entity (P)"
          >&#8592;</button>
          <button
            type="button"
            class="p-link text-color-secondary"
            (click)="nextEntity()"
            [attr.aria-label]="'Next entity'"
            title="Next entity (N)"
          >&#8594;</button>
        </span>
      </div>
    }

    <!-- No entities state -->
    @if (pages().length > 0 && allEntityTypes().length === 0) {
      <div class="surface-100 border-round p-3 text-center text-color-secondary text-sm mb-3">
        No clinical entities detected in this document.
      </div>
    }

    <!-- Pages with highlighted text -->
    @for (page of pages(); track page.pageNumber) {
      <p-panel
        [header]="pageHeader(page)"
        [toggleable]="true"
        styleClass="mb-2"
      >
        <div
          class="white-space-pre-wrap"
          style="font-family: inherit; font-size: 0.9rem; line-height: 1.8"
        >
          @for (seg of segmentsForPage(page); track $index) {
            @if (seg.kind === 'plain') {
              {{ seg.text }}
            } @else {
              <span
                #entitySpan
                [attr.data-entity-index]="asEntity(seg).index"
                class="cursor-pointer"
                [style.background]="typeColor(asEntity(seg).entity.type) + '26'"
                [style.border-bottom]="entityBorder(asEntity(seg).entity)"
                [style.border-radius]="'2px'"
                [style.padding]="'1px 0'"
                [class.ring-2]="focusedIndex() === asEntity(seg).index"
                [pTooltip]="entityTooltip(asEntity(seg).entity)"
                tooltipPosition="top"
                (click)="focusEntity(asEntity(seg).index)"
              >{{ asEntity(seg).text }}</span>
            }
          }
          @if (!segmentsForPage(page).length) {
            <span class="text-color-secondary">(No text detected on this page)</span>
          }
        </div>
      </p-panel>
    }

    <!-- Entity summary panel -->
    @if (allEntityTypes().length > 0) {
      <p-divider />
      <div class="mt-2">
        <h3 class="text-base font-semibold mb-2">Entity Summary</h3>
        @for (entry of entityGroups() | keyvalue; track entry.key) {
          <div class="mb-2">
            <span
              class="text-xs font-semibold uppercase px-2 py-1 border-round"
              [style.background]="typeColor(entry.key) + '33'"
              [style.color]="typeColor(entry.key)"
            >{{ entry.key }} ({{ entry.value.length }})</span>
            <div class="flex flex-wrap gap-1 mt-1">
              @for (ent of entry.value; track ent.startOffset + '-' + ent.pageNumber) {
                <span
                  class="text-sm border-round px-2 py-1 cursor-pointer"
                  [style.background]="typeColor(entry.key) + '1A'"
                  [style.border-bottom]="entityBorder(ent)"
                  [pTooltip]="entityTooltip(ent)"
                  tooltipPosition="top"
                  (click)="scrollToEntity(ent)"
                >{{ ent.text }}</span>
              }
            </div>
          </div>
        }
      </div>
    }
  `,
})
export class DocumentViewerComponent {
  private readonly highlighter = inject(EntityHighlighterService);
  private readonly elRef = inject(ElementRef);

  /** All OCR pages to display. */
  readonly pages = input.required<OcrPageResult[]>();

  /** All NER entities (flat list, all pages). */
  readonly entities = input.required<NerEntity[]>();

  // ── State ─────────────────────────────────────────────────────────────────
  readonly focusedIndex = signal(-1);

  /** Map of entity type → enabled flag. */
  readonly typeEnabled = signal<Record<string, boolean>>({});

  readonly entitySpanElements = viewChildren<ElementRef>('entitySpan');

  // ── Derived ───────────────────────────────────────────────────────────────

  /** All unique entity types present in this document. */
  readonly allEntityTypes = computed(() =>
    [...new Set(this.entities().map((e) => e.type))].sort(),
  );

  /** Entity count per type (all, regardless of toggle). */
  readonly entityCountByType = computed(() =>
    this.entities().reduce(
      (acc, e) => {
        acc[e.type] = (acc[e.type] ?? 0) + 1;
        return acc;
      },
      {} as Record<string, number>,
    ),
  );

  /** Currently visible entities (active types only). */
  readonly visibleEntities = computed(() => {
    const enabled = this.typeEnabled();
    return this.entities().filter((e) => enabled[e.type] !== false);
  });

  readonly totalVisible = computed(() => this.visibleEntities().length);

  /** Entities grouped by type (visible only). */
  readonly entityGroups = computed(() =>
    this.visibleEntities().reduce(
      (acc, e) => {
        (acc[e.type] ??= []).push(e);
        return acc;
      },
      {} as Record<string, NerEntity[]>,
    ),
  );

  // ── Lifecycle ─────────────────────────────────────────────────────────────

  ngOnInit(): void {
    // Enable all types by default.
    const enabled: Record<string, boolean> = {};
    this.allEntityTypes().forEach((t) => (enabled[t] = true));
    this.typeEnabled.set(enabled);
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  typeColor(type: string): string {
    return ENTITY_COLORS[type] ?? 'var(--surface-500)';
  }

  entityBorder(entity: NerEntity): string {
    const color = this.typeColor(entity.type);
    const style = entity.lowConfidence ? 'dashed' : 'solid';
    return `2px ${style} ${color}`;
  }

  entityTooltip(entity: NerEntity): string {
    const conf = (entity.confidenceScore * 100).toFixed(0);
    const flag = entity.lowConfidence ? ' ⚠ low confidence' : '';
    return `${entity.type} · ${conf}%${flag}`;
  }

  pageHeader(page: OcrPageResult): string {
    const count = this.visibleEntities().filter(
      (e) => e.pageNumber === page.pageNumber || e.pageNumber === 0,
    ).length;
    const entPart = count > 0 ? ` · ${count} entit${count === 1 ? 'y' : 'ies'}` : '';
    return `Page ${page.pageNumber}${entPart}`;
  }

  segmentsForPage(page: OcrPageResult): TextSegment[] {
    const pageEntities = this.visibleEntities().filter(
      (e) => e.pageNumber === page.pageNumber || e.pageNumber === 0,
    );
    // Calculate offset of first entity index for this page for keyboard nav continuity.
    const offset = this.visibleEntities().findIndex(
      (e) => e.pageNumber === page.pageNumber || e.pageNumber === 0,
    );
    return this.highlighter.buildSegments(page.text, pageEntities, Math.max(0, offset));
  }

  asEntity(seg: TextSegment): EntitySegment {
    return seg as EntitySegment;
  }

  onTypeToggle(type: string, checked: boolean): void {
    this.typeEnabled.update((prev) => ({ ...prev, [type]: checked }));
    this.focusedIndex.set(-1);
  }

  // ── Keyboard navigation ───────────────────────────────────────────────────

  @HostListener('document:keydown', ['$event'])
  onKeyDown(event: KeyboardEvent): void {
    const tag = (event.target as HTMLElement).tagName.toLowerCase();
    if (tag === 'input' || tag === 'textarea') return;
    if (event.key === 'n') this.nextEntity();
    if (event.key === 'p') this.prevEntity();
  }

  nextEntity(): void {
    const total = this.totalVisible();
    if (!total) return;
    this.focusedIndex.update((i) => (i + 1) % total);
    this.scrollToFocused();
  }

  prevEntity(): void {
    const total = this.totalVisible();
    if (!total) return;
    this.focusedIndex.update((i) => (i - 1 + total) % total);
    this.scrollToFocused();
  }

  focusEntity(index: number): void {
    this.focusedIndex.set(index);
  }

  scrollToEntity(entity: NerEntity): void {
    const idx = this.visibleEntities().indexOf(entity);
    if (idx >= 0) {
      this.focusedIndex.set(idx);
      this.scrollToFocused();
    }
  }

  private scrollToFocused(): void {
    const idx = this.focusedIndex();
    const el = this.elRef.nativeElement.querySelector(
      `[data-entity-index="${idx}"]`,
    ) as HTMLElement | null;
    el?.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }
}
```

---

### 4. Update `DocumentDetailComponent` — Use Viewer

Edit `src/health-platform-ui/src/app/features/clinical/documents/document-detail.component.ts`.

Replace the plain text pages section and old entity badges block with
`<app-document-viewer>`:

**Remove** these imports (now moved to viewer):
- `DecimalPipe`, `PercentPipe`, `PanelModule`, `TooltipModule`
- `entityGroups` signal, `objectEntries`, `groupByType` method

**Add** import:
```typescript
import { DocumentViewerComponent } from './document-viewer.component';
```

**Simplify** the `imports` array to:
```typescript
imports: [ButtonModule, CardModule, DocumentViewerComponent, SkeletonModule, TagModule, RouterLink],
```

**Remove** the `entityGroups` signal, `objectEntries` property, and `groupByType()` method from the class.

**Replace** the entire `<!-- Extracted text pages -->` section and the `<!-- Named Entities -->` section with:

```html
<!-- Document Viewer (pages + entity highlights + summary) -->
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

> The viewer handles the "no entities" empty state internally, so the outer
> template no longer needs a separate entity section.

---

## File Checklist

| File                                                                                             | Action |
|--------------------------------------------------------------------------------------------------|--------|
| `src/health-platform-ui/src/app/core/models/document.models.ts`                                  | Modify — add `pageNumber: number` to `NerEntity` |
| `src/health-platform-ui/src/app/core/services/entity-highlighter.service.ts`                     | Create |
| `src/health-platform-ui/src/app/features/clinical/documents/document-viewer.component.ts`        | Create |
| `src/health-platform-ui/src/app/features/clinical/documents/document-detail.component.ts`        | Modify — replace text/entity sections with `<app-document-viewer>` |

## Verification

```bash
# Angular TypeScript — 0 errors
cd src/health-platform-ui && npx tsc --noEmit

# Lint — 0 errors
npx ng lint

# Manual smoke test:
# 1. Open /clinical/documents/{id} for a Processed document
# 2. Verify entities are highlighted with correct colours
# 3. Toggle DIAGNOSIS checkbox — DIAGNOSIS highlights disappear
# 4. Press 'n' / 'p' to navigate between highlighted entities
# 5. Click an entity → tooltip shows type + confidence %
# 6. Low-confidence entity → dashed underline
# 7. Open a document with no entities → "No clinical entities detected"
```

## Architecture Notes

- `EntityHighlighterService` only produces `TextSegment[]` from text + entities —
  zero DOM manipulation, XSS-safe (`textContent` binding only, never `innerHTML`)
- `DocumentViewerComponent` is a pure-display component; all data flows in via
  `input()` signals — no HTTP calls, no DI of services except `EntityHighlighterService`
- `viewChildren('entitySpan')` gives a query list for programmatic scroll-to —
  used only when the user navigates via keyboard
- `CheckboxModule` from PrimeNG 21: `[(ngModel)]` requires `FormsModule` OR the
  `[binary]="true"` + `(onChange)` pattern used here (no FormsModule needed)
- `KeyValuePipe` is used for the entity summary `@for (entry of entityGroups() | keyvalue)` iteration
