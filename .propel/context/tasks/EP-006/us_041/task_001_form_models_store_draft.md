# Task 001: Angular Form Models, Store & Draft Service

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-041 |
| **Epic** | EP-006 |
| **Layer** | Angular — models, store, draft service |
| **Priority** | High |
| **Estimated Effort** | 25 minutes |
| **Dependencies** | US-040 Task 003 complete — `intake.models.ts`, `IntakeChatStore`, `intake.routes.ts` all in place |

## Objective

1. **Extend `intake.models.ts`** — add `IntakeFormData` (the six-section payload) and `IntakeFormDraft` (localStorage envelope with TTL)
2. **Create `IntakeFormStore`** (ngrx/signals) — reactive state for all form sections, computed progress signal, draft persistence, and `toCollected()` output mapper that produces the same `Record<string, string | null>` as `IntakeChatStore`
3. **Static suggestion lists** — `MEDICATION_SUGGESTIONS` and `ALLERGY_SUGGESTIONS` exported constants (used by Task 002's autocomplete inputs)
4. **3 unit tests** — initial state, progress computation, draft save/load round-trip

---

## Acceptance Criteria Covered

- AC: Save draft functionality (resume later before appointment)
- AC: Form data maps to same structured output as conversational intake
- AC: Draft saved but appointment cancelled → draft purged after 7 days (TTL check on load)

---

## Design Notes

### Draft storage

Drafts are stored in `localStorage` under the key `intake:draft` (or `intake:draft:{appointmentId}` when an appointment ID is available). On load, check `savedAt` timestamp — if older than 7 days, discard automatically.

```typescript
interface IntakeFormDraft {
  data: IntakeFormData;
  savedAt: number;         // Date.now() ms
  appointmentId?: string;
}
const DRAFT_TTL_MS = 7 * 24 * 60 * 60 * 1000; // 7 days
```

### Progress computation

```typescript
// Count non-empty required + optional fields
const total = 6;  // one per section
const filled = [chiefComplaint, symptoms.length, duration, medications.length, allergies.length, medicalHistory]
  .filter(v => (typeof v === 'string' ? v.trim() !== '' : v > 0)).length;
progress = Math.round((filled / total) * 100);
```

### `toCollected()` output shape

Maps directly to `IntakeChatStore.collected()` so both modes produce the same downstream payload:

```typescript
{
  chief_complaint: string | null,
  symptom_duration: string | null,   // duration + " — severity " + severity
  severity: string | null,           // "5"
  medications: string | null,        // joined array
  allergies: string | null,          // joined array
  medical_history: string | null,
}
```

---

## Implementation Steps

### 1. Extend `src/health-platform-ui/src/app/core/models/intake.models.ts`

Append after existing interfaces:

```typescript
// --- Structured Form ---

export interface IntakeFormData {
  chiefComplaint: string;
  symptoms: string[];
  duration: string;
  severity: number;          // 1–10
  medications: string[];
  allergies: string[];
  medicalHistory: string;
}

export interface IntakeFormDraft {
  data: IntakeFormData;
  savedAt: number;
  appointmentId?: string;
}
```

### 2. Create `src/health-platform-ui/src/app/features/intake/intake-form.store.ts`

```typescript
import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { IntakeFormData, IntakeFormDraft } from '../../core/models/intake.models';
import { ToastService } from '../../shared/services/toast.service';

export const MEDICATION_SUGGESTIONS = [
  'Aspirin', 'Ibuprofen', 'Paracetamol', 'Metformin', 'Atorvastatin',
  'Lisinopril', 'Amlodipine', 'Omeprazole', 'Simvastatin', 'Metoprolol',
  'Amoxicillin', 'Azithromycin', 'Levothyroxine', 'Gabapentin', 'Sertraline',
];

export const ALLERGY_SUGGESTIONS = [
  'Penicillin', 'Aspirin', 'Ibuprofen', 'Sulfa drugs', 'Codeine',
  'Latex', 'Peanuts', 'Tree nuts', 'Shellfish', 'Eggs',
  'Milk', 'Soy', 'Wheat', 'Bee stings', 'Contrast dye',
];

const DRAFT_KEY = 'intake:draft';
const DRAFT_TTL_MS = 7 * 24 * 60 * 60 * 1000;

const emptyForm = (): IntakeFormData => ({
  chiefComplaint: '',
  symptoms: [],
  duration: '',
  severity: 5,
  medications: [],
  allergies: [],
  medicalHistory: '',
});

interface IntakeFormState {
  form: IntakeFormData;
  isDirty: boolean;
  isSubmitted: boolean;
}

const initialState: IntakeFormState = {
  form: emptyForm(),
  isDirty: false,
  isSubmitted: false,
};

export const IntakeFormStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed((store) => ({
    progress: computed(() => {
      const f = store.form();
      const filled = [
        f.chiefComplaint.trim() !== '',
        f.symptoms.length > 0,
        f.duration.trim() !== '',
        f.medications.length > 0 || f.medicalHistory.trim() !== '',
        f.allergies.length > 0,
        f.medicalHistory.trim() !== '',
      ].filter(Boolean).length;
      return Math.round((filled / 6) * 100);
    }),
  })),
  withMethods((store, toast = inject(ToastService)) => ({
    patch(partial: Partial<IntakeFormData>): void {
      patchState(store, { form: { ...store.form(), ...partial }, isDirty: true });
    },

    prefill(collected: Record<string, string | null>): void {
      patchState(store, {
        form: {
          ...store.form(),
          chiefComplaint: collected['chief_complaint'] ?? '',
          duration: collected['symptom_duration'] ?? '',
          severity: collected['severity'] ? Number(collected['severity']) : 5,
          medications: collected['medications']
            ? collected['medications'].split(',').map((s) => s.trim()).filter(Boolean)
            : [],
          allergies: collected['allergies']
            ? collected['allergies'].split(',').map((s) => s.trim()).filter(Boolean)
            : [],
          medicalHistory: collected['medical_history'] ?? '',
        },
        isDirty: false,
      });
    },

    toCollected(): Record<string, string | null> {
      const f = store.form();
      return {
        chief_complaint: f.chiefComplaint.trim() || null,
        symptom_duration: f.duration.trim()
          ? `${f.duration.trim()} — severity ${f.severity}`
          : null,
        severity: String(f.severity),
        medications: f.medications.length ? f.medications.join(', ') : null,
        allergies: f.allergies.length ? f.allergies.join(', ') : null,
        medical_history: f.medicalHistory.trim() || null,
      };
    },

    saveDraft(appointmentId?: string): void {
      const draft: IntakeFormDraft = {
        data: store.form(),
        savedAt: Date.now(),
        appointmentId,
      };
      localStorage.setItem(DRAFT_KEY, JSON.stringify(draft));
      patchState(store, { isDirty: false });
      toast.success('Draft saved', 'Your intake progress has been saved.');
    },

    loadDraft(): boolean {
      const raw = localStorage.getItem(DRAFT_KEY);
      if (!raw) return false;
      try {
        const draft = JSON.parse(raw) as IntakeFormDraft;
        if (Date.now() - draft.savedAt > DRAFT_TTL_MS) {
          localStorage.removeItem(DRAFT_KEY);
          return false;
        }
        patchState(store, { form: draft.data, isDirty: false });
        return true;
      } catch {
        localStorage.removeItem(DRAFT_KEY);
        return false;
      }
    },

    clearDraft(): void {
      localStorage.removeItem(DRAFT_KEY);
      patchState(store, { form: emptyForm(), isDirty: false, isSubmitted: false });
    },

    markSubmitted(): void {
      patchState(store, { isSubmitted: true });
    },

    reset(): void {
      patchState(store, initialState);
    },
  })),
);
```

### 3. Create `src/health-platform-ui/src/app/features/intake/intake-form.store.spec.ts`

```typescript
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { IntakeFormStore } from './intake-form.store';

describe('IntakeFormStore', () => {
  let store: InstanceType<typeof IntakeFormStore>;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideNoopAnimations(), MessageService, IntakeFormStore],
    });
    store = TestBed.inject(IntakeFormStore);
  });

  it('should have empty initial state with 0 progress', () => {
    expect(store.form().chiefComplaint).toBe('');
    expect(store.progress()).toBe(0);
    expect(store.isDirty()).toBe(false);
  });

  it('should compute progress when fields are filled', () => {
    store.patch({ chiefComplaint: 'Chest pain', symptoms: ['pain'], duration: '2 days' });
    expect(store.progress()).toBeGreaterThan(0);
  });

  it('should round-trip saveDraft and loadDraft', () => {
    store.patch({ chiefComplaint: 'Headache', severity: 7 });
    store.saveDraft();
    store.reset();
    expect(store.form().chiefComplaint).toBe('');
    const loaded = store.loadDraft();
    expect(loaded).toBe(true);
    expect(store.form().chiefComplaint).toBe('Headache');
    expect(store.form().severity).toBe(7);
  });
});
```

---

## Verification

```bash
cd src/health-platform-ui
npx ng test --no-watch
```

Expected: all existing tests pass + 3 new `IntakeFormStore` tests (total ≥ 21).
