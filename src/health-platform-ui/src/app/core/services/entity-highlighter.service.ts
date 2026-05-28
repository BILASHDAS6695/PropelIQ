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
  buildSegments(pageText: string, entities: NerEntity[], entityOffset = 0): TextSegment[] {
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
