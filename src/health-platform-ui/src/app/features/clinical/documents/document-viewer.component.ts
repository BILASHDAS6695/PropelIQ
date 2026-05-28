import { KeyValuePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  Component,
  computed,
  ElementRef,
  HostListener,
  inject,
  input,
  OnInit,
  signal,
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
  DIAGNOSIS: 'var(--red-400)',
  MEDICATION: 'var(--blue-400)',
  PROCEDURE: 'var(--green-400)',
  LAB_TEST: 'var(--purple-400)',
  LAB_VALUE: 'var(--purple-300)',
  ANATOMY: 'var(--teal-400)',
  SYMPTOM: 'var(--orange-400)',
};

@Component({
  selector: 'app-document-viewer',
  standalone: true,
  imports: [
    CheckboxModule,
    DividerModule,
    FormsModule,
    KeyValuePipe,
    PanelModule,
    TagModule,
    TooltipModule,
  ],
  template: `
    <!-- Entity type toggle legend -->
    @if (allEntityTypes().length > 0) {
      <div class="flex flex-wrap gap-3 mb-3 align-items-center">
        <span class="text-sm font-semibold text-color-secondary">Highlights:</span>
        @for (type of allEntityTypes(); track type) {
          <label [for]="'chk-' + type" class="flex align-items-center gap-1 cursor-pointer text-sm">
            <p-checkbox
              [binary]="true"
              [inputId]="'chk-' + type"
              [ngModel]="typeEnabled()[type] !== false"
              (onChange)="onTypeToggle(type, $event.checked)"
            />
            <span
              class="px-1 border-round"
              [style.background]="typeColor(type) + '33'"
              [style.border-bottom]="'2px solid ' + typeColor(type)"
            >
              {{ type }}
            </span>
            <span class="text-color-secondary">({{ entityCountByType()[type] || 0 }})</span>
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
          >
            &#8592;
          </button>
          <button
            type="button"
            class="p-link text-color-secondary"
            (click)="nextEntity()"
            [attr.aria-label]="'Next entity'"
            title="Next entity (N)"
          >
            &#8594;
          </button>
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
      <p-panel [header]="pageHeader(page)" [toggleable]="true" styleClass="mb-2">
        <div
          class="white-space-pre-wrap"
          style="font-family: inherit; font-size: 0.9rem; line-height: 1.8"
        >
          @for (seg of segmentsForPage(page); track $index) {
            @if (seg.kind === 'plain') {
              {{ seg.text }}
            } @else {
              <span
                [attr.data-entity-index]="asEntity(seg).index"
                class="cursor-pointer"
                tabindex="0"
                role="button"
                [style.background]="typeColor(asEntity(seg).entity.type) + '26'"
                [style.border-bottom]="entityBorder(asEntity(seg).entity)"
                [style.border-radius]="'2px'"
                [style.padding]="'1px 0'"
                [class.ring-2]="focusedIndex() === asEntity(seg).index"
                [pTooltip]="entityTooltip(asEntity(seg).entity)"
                tooltipPosition="top"
                (click)="focusEntity(asEntity(seg).index)"
                (keydown.enter)="focusEntity(asEntity(seg).index)"
                >{{ asEntity(seg).text }}</span
              >
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
            >
              {{ entry.key }} ({{ entry.value.length }})
            </span>
            <div class="flex flex-wrap gap-1 mt-1">
              @for (ent of entry.value; track ent.startOffset + '-' + ent.pageNumber) {
                <span
                  class="text-sm border-round px-2 py-1 cursor-pointer"
                  tabindex="0"
                  role="button"
                  [style.background]="typeColor(entry.key) + '1A'"
                  [style.border-bottom]="entityBorder(ent)"
                  [pTooltip]="entityTooltip(ent)"
                  tooltipPosition="top"
                  (click)="scrollToEntity(ent)"
                  (keydown.enter)="scrollToEntity(ent)"
                >
                  {{ ent.text }}
                </span>
              }
            </div>
          </div>
        }
      </div>
    }
  `,
})
export class DocumentViewerComponent implements OnInit {
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

  // ── Derived ───────────────────────────────────────────────────────────────

  /** All unique entity types present in this document. */
  readonly allEntityTypes = computed(() => [...new Set(this.entities().map((e) => e.type))].sort());

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
