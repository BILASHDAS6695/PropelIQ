# Task 003: Angular Submit Wiring, Draft Auto-save & Intake Summary UI

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-042 |
| **Epic** | EP-006 |
| **Layer** | Angular Frontend — service, store, summary component, routing |
| **Priority** | High |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | Task 002 complete — API endpoints `/intake/draft`, `/intake/submit`, `/intake/{appointmentId}`, `/intake/{appointmentId}/reviewed` are live |

## Objective

1. **Extend `IntakeService`** — add `saveDraft()`, `submitIntake()`, `getIntakeSummary()`, `markReviewed()` methods
2. **Extend `IntakeFormStore`** — add `submitToBackend()` and `saveDraftToBackend()` methods (call service, update `isSubmitted`)
3. **Add `IntakeSummaryComponent`** — formatted read-only display of `IntakeSummaryDto` for provider/patient summary view
4. **Update `intake.routes.ts`** — add `/intake/summary/:appointmentId` lazy route
5. **1 smoke test** for `IntakeSummaryComponent`

---

## Acceptance Criteria Covered

- AC: Partial intake saved as Draft (patient can resume)
- AC: Completed intake immutable (no patient edits after submission)
- AC: Intake summary view: formatted display of structured data (not raw JSON)
- AC: Provider can mark intake as "Reviewed" (timestamp + providerId)

---

## Design Notes

### New interfaces in `intake.models.ts`

```typescript
export interface IntakeSummaryDto {
  id: string;
  appointmentId: string;
  patientId: string;
  mode: 'AiConversational' | 'ManualForm';
  status: 'Draft' | 'Completed' | 'ReviewedByProvider' | 'Orphaned';
  data: IntakeFormData | null;
  completedAt: string | null;
  reviewedAt: string | null;
  reviewedByProviderId: string | null;
}

export interface IntakeSubmitRequest {
  appointmentId: string;
  mode: 'AiConversational' | 'ManualForm';
  data: IntakeFormData;
}
```

### `IntakeService` additions

All methods hit `${environment.apiUrl}/intake/...`:

```typescript
saveDraft(req: IntakeSubmitRequest): Observable<{ id: string }> {
  return this.http.post<{ id: string }>(`${this.base}/draft`, req);
}

submitIntake(req: IntakeSubmitRequest): Observable<{ id: string }> {
  return this.http.post<{ id: string }>(`${this.base}/submit`, req);
}

getIntakeSummary(appointmentId: string): Observable<IntakeSummaryDto> {
  return this.http.get<IntakeSummaryDto>(`${this.base}/${appointmentId}`);
}

markReviewed(appointmentId: string): Observable<void> {
  return this.http.put<void>(`${this.base}/${appointmentId}/reviewed`, {});
}
```

### `IntakeFormStore` additions

Two new methods using `inject(IntakeService)` and `inject(ToastService)`:

```typescript
async submitToBackend(appointmentId: string, mode: 'AiConversational' | 'ManualForm'): Promise<void> {
  const req: IntakeSubmitRequest = {
    appointmentId,
    mode,
    data: store.form(),
  };
  try {
    await firstValueFrom(intakeService.submitIntake(req));
    store.markSubmitted();
    toast.success('Intake submitted', 'Your intake form has been received.');
  } catch {
    toast.error('Submission failed', 'Please try again.');
  }
}

async saveDraftToBackend(appointmentId: string, mode: 'AiConversational' | 'ManualForm'): Promise<void> {
  const req: IntakeSubmitRequest = {
    appointmentId,
    mode,
    data: store.form(),
  };
  try {
    await firstValueFrom(intakeService.saveDraft(req));
    store.saveDraft(appointmentId);          // also persist to localStorage
  } catch {
    toast.error('Draft save failed', 'Your draft could not be saved remotely.');
  }
}
```

### `IntakeSummaryComponent`

Selector: `app-intake-summary`  
Route: `/intake/summary/:appointmentId`  
File: `src/health-platform-ui/src/app/features/intake/intake-summary/intake-summary.component.ts`

- On init: calls `IntakeService.getIntakeSummary(appointmentId)` from route params
- Displays `p-tag` for status (Draft=warn, Completed=success, ReviewedByProvider=info, Orphaned=danger)
- Each field section (Chief Complaint, Symptoms, Duration, Severity, Medications, Allergies, Medical History) displayed as read-only rows
- "Mark as Reviewed" `p-button` shown when `status === 'Completed'` and user is in Provider role
  - On click: calls `IntakeService.markReviewed(appointmentId)` → refreshes data
- Uses `SkeletonModule` during loading, `ChangeDetectionStrategy.OnPush`

---

## Implementation Steps

### 1. Extend `intake.models.ts`

Append to `src/health-platform-ui/src/app/core/models/intake.models.ts`:

```typescript
export type IntakeMode = 'AiConversational' | 'ManualForm';
export type IntakeStatus = 'Draft' | 'Completed' | 'ReviewedByProvider' | 'Orphaned';

export interface IntakeSummaryDto {
  id: string;
  appointmentId: string;
  patientId: string;
  mode: IntakeMode;
  status: IntakeStatus;
  data: IntakeFormData | null;
  completedAt: string | null;
  reviewedAt: string | null;
  reviewedByProviderId: string | null;
}

export interface IntakeSubmitRequest {
  appointmentId: string;
  mode: IntakeMode;
  data: IntakeFormData;
}
```

### 2. Update `IntakeService`

Add to `src/health-platform-ui/src/app/core/services/intake.service.ts`:

```typescript
private readonly base = `${environment.apiUrl}/intake`;

saveDraft(req: IntakeSubmitRequest): Observable<{ id: string }> {
  return this.http.post<{ id: string }>(`${this.base}/draft`, req);
}

submitIntake(req: IntakeSubmitRequest): Observable<{ id: string }> {
  return this.http.post<{ id: string }>(`${this.base}/submit`, req);
}

getIntakeSummary(appointmentId: string): Observable<IntakeSummaryDto> {
  return this.http.get<IntakeSummaryDto>(`${this.base}/${appointmentId}`);
}

markReviewed(appointmentId: string): Observable<void> {
  return this.http.put<void>(`${this.base}/${appointmentId}/reviewed`, {});
}
```

Note: read the current `intake.service.ts` before editing — preserve the existing `chat()` method and `private readonly base` field if it already exists; only add the missing methods.

### 3. Update `IntakeFormStore`

Add `submitToBackend` and `saveDraftToBackend` methods to the `withMethods` block in `intake-form.store.ts`. Inject `IntakeService` alongside `ToastService`. Use `firstValueFrom` from `rxjs`.

Import additions needed:
```typescript
import { firstValueFrom } from 'rxjs';
import { IntakeService } from '../../core/services/intake.service';
import { IntakeSubmitRequest } from '../../core/models/intake.models';
```

### 4. Create `IntakeSummaryComponent`

Create `src/health-platform-ui/src/app/features/intake/intake-summary/intake-summary.component.ts`:

```typescript
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DividerModule } from 'primeng/divider';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';
import { IntakeSummaryDto } from '../../../core/models/intake.models';
import { IntakeService } from '../../../core/services/intake.service';
import { ToastService } from '../../../shared/services/toast.service';

type TagSeverity = 'success' | 'info' | 'warn' | 'danger' | 'secondary';

@Component({
  selector: 'app-intake-summary',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, ButtonModule, DividerModule, SkeletonModule, TagModule],
  template: `
    <div class="summary-page">
      <h2 class="text-xl font-semibold mb-3">
        <i class="pi pi-file-check mr-2"></i>Intake Summary
      </h2>

      @if (isLoading()) {
        <p-skeleton height="2rem" styleClass="mb-2" />
        <p-skeleton height="8rem" />
      } @else if (summary()) {
        <div class="flex align-items-center gap-2 mb-3">
          <p-tag
            [value]="summary()!.status"
            [severity]="statusSeverity(summary()!.status)"
          />
          @if (summary()!.completedAt) {
            <span class="text-sm text-color-secondary">
              Completed {{ summary()!.completedAt | date: 'medium' }}
            </span>
          }
          @if (summary()!.reviewedAt) {
            <span class="text-sm text-color-secondary">
              &bull; Reviewed {{ summary()!.reviewedAt | date: 'medium' }}
            </span>
          }
        </div>

        @if (summary()!.data; as d) {
          <section class="summary-section">
            <h3 class="summary-label">Chief Complaint</h3>
            <p>{{ d.chiefComplaint || '—' }}</p>
          </section>
          <p-divider />
          <section class="summary-section">
            <h3 class="summary-label">Symptoms</h3>
            <p>{{ d.symptoms?.length ? d.symptoms.join(', ') : '—' }}</p>
          </section>
          <p-divider />
          <section class="summary-section">
            <h3 class="summary-label">Duration &amp; Severity</h3>
            <p>{{ d.duration || '—' }} — Severity {{ d.severity }}/10</p>
          </section>
          <p-divider />
          <section class="summary-section">
            <h3 class="summary-label">Medications</h3>
            <p>{{ d.medications?.length ? d.medications.join(', ') : 'None reported' }}</p>
          </section>
          <p-divider />
          <section class="summary-section">
            <h3 class="summary-label">Allergies</h3>
            <p>{{ d.allergies?.length ? d.allergies.join(', ') : 'None reported' }}</p>
          </section>
          <p-divider />
          <section class="summary-section">
            <h3 class="summary-label">Medical History</h3>
            <p>{{ d.medicalHistory || '—' }}</p>
          </section>
        } @else {
          <p class="text-color-secondary">No intake data recorded.</p>
        }

        @if (summary()!.status === 'Completed') {
          <p-divider />
          <p-button
            label="Mark as Reviewed"
            icon="pi pi-check-circle"
            severity="success"
            [loading]="isReviewing()"
            (onClick)="markReviewed()"
            aria-label="Mark intake as reviewed by provider"
          />
        }
      } @else {
        <p class="text-color-secondary">No intake record found for this appointment.</p>
      }
    </div>
  `,
  styles: [`
    .summary-page { max-width: 720px; margin: 0 auto; padding: 1.5rem 1rem; }
    .summary-section { margin-bottom: 0.25rem; }
    .summary-label { font-size: 0.875rem; font-weight: 600; color: var(--text-color-secondary); margin-bottom: 0.25rem; text-transform: uppercase; letter-spacing: 0.05em; }
  `],
})
export class IntakeSummaryComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly intakeService = inject(IntakeService);
  private readonly toast = inject(ToastService);

  protected readonly summary = signal<IntakeSummaryDto | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isReviewing = signal(false);

  private appointmentId = '';

  ngOnInit(): void {
    this.appointmentId = this.route.snapshot.paramMap.get('appointmentId') ?? '';
    this.loadSummary();
  }

  protected statusSeverity(status: string): TagSeverity {
    const map: Record<string, TagSeverity> = {
      Draft: 'warn',
      Completed: 'success',
      ReviewedByProvider: 'info',
      Orphaned: 'danger',
    };
    return map[status] ?? 'secondary';
  }

  protected async markReviewed(): Promise<void> {
    this.isReviewing.set(true);
    try {
      await new Promise<void>((resolve, reject) =>
        this.intakeService.markReviewed(this.appointmentId).subscribe({
          next: () => resolve(),
          error: (e) => reject(e),
        }),
      );
      this.toast.success('Intake reviewed', 'Marked as reviewed successfully.');
      this.loadSummary();
    } catch {
      this.toast.error('Review failed', 'Could not mark intake as reviewed.');
    } finally {
      this.isReviewing.set(false);
    }
  }

  private loadSummary(): void {
    this.isLoading.set(true);
    this.intakeService.getIntakeSummary(this.appointmentId).subscribe({
      next: (dto) => {
        this.summary.set(dto);
        this.isLoading.set(false);
      },
      error: () => {
        this.summary.set(null);
        this.isLoading.set(false);
      },
    });
  }
}
```

### 5. Update `intake.routes.ts`

Add the summary route:

```typescript
{
  path: 'summary/:appointmentId',
  loadComponent: () =>
    import('./intake-summary/intake-summary.component').then(
      (m) => m.IntakeSummaryComponent,
    ),
},
```

### 6. Create `intake-summary.component.spec.ts`

Create `src/health-platform-ui/src/app/features/intake/intake-summary/intake-summary.component.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { IntakeSummaryComponent } from './intake-summary.component';

describe('IntakeSummaryComponent', () => {
  let fixture: ComponentFixture<IntakeSummaryComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [IntakeSummaryComponent],
      providers: [
        provideHttpClient(),
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(IntakeSummaryComponent);
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
```

---

## Verification

```bash
cd src/health-platform-ui
npx ng build
npx ng lint
npx ng test --no-watch
```

Expected:
- Build clean
- Lint clean
- Tests ≥ 23 (22 existing + 1 summary smoke test)
