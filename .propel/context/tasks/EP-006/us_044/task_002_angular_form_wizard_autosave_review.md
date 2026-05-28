# Task 002: Angular — Multi-Step Form Wizard, Auto-Save & Submission Review

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-044 |
| **Epic** | EP-006 |
| **Layer** | Angular Frontend — `IntakeLandingComponent` |
| **Priority** | High |
| **Estimated Effort** | 35 minutes |
| **Dependencies** | Task 001 complete (or parallel — no shared files) |

## Objective

1. **Convert `IntakeLandingComponent`** from a single scrollable form to a 3-step wizard with a step indicator and Back / Next navigation
2. **Add auto-save** — every 30 seconds call `store.saveDraft()` silently if form is dirty (cleared in `ngOnDestroy`)
3. **Rename "Save Draft" → "Save & Continue Later"** to match the acceptance criteria label
4. **Read `appointmentId` from query params** and thread it through `saveDraft()` and `submitToBackend()` calls
5. **Fix `submit()`** — call `store.submitToBackend(appointmentId, 'ManualForm')` and navigate to `/intake/summary/:appointmentId`
6. **Add submission review panel at step 3** — shows all entered data read-only; "Edit" resets to step 1; "Confirm & Submit" triggers `submit()`
7. **Add responsive CSS** to `IntakeLandingComponent` for mobile viewports

---

## Acceptance Criteria Covered

- AC: Form interface: multi-step wizard with progress bar
- AC: Submission confirmation: summary of provided data with "Edit" and "Submit" buttons
- AC: "Save & Continue Later" button persists draft
- AC: Mobile-optimized (form max-width 100% on small screens)
- AC: Patient accidentally closes tab → draft auto-saved every 30 seconds
- AC: Accessible: keyboard navigable, screen reader compatible, WCAG 2.1 AA

---

## Design Notes

### Wizard steps

| Step | Sections | Description |
|------|----------|-------------|
| 1 of 3 | 1–2 | Chief Complaint + Symptoms |
| 2 of 3 | 3–5 | Duration / Severity + Medications + Allergies |
| 3 of 3 | 6 + Review | Medical History + inline review summary |

The `p-progressBar [value]="store.progress()"` already reflects field completion — no change needed. The step indicator is an additional visual cue above the progress bar.

### Step indicator

Use `p-tag` badges: current step → `severity="info"`, completed step → `severity="success"`, future step → `severity="secondary"`.

### Review panel (step 3, below Medical History section)

After the Medical History textarea, show an inline summary section with a grey card listing all entered data. Two action buttons at the bottom:
- **"Edit"** (`severity="secondary"`, `icon="pi pi-pencil"`) → `currentStep.set(1)` (goes back to step 1 for full re-edit)
- **"Confirm & Submit"** (`icon="pi pi-check"`) → calls `submit()`

The "Save & Continue Later" button remains available at every step.

---

## Implementation Steps

### 1. Add `OnDestroy`, `ActivatedRoute` and step signals

Open `src/health-platform-ui/src/app/features/intake/intake-landing/intake-landing.component.ts`.

**a) Add imports:**

```typescript
import { ChangeDetectionStrategy, Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
```

**b) Update `implements` clause:**

```typescript
export class IntakeLandingComponent implements OnInit, OnDestroy {
```

**c) Inject `ActivatedRoute` and `Router`:**

```typescript
private readonly route = inject(ActivatedRoute);
private readonly router = inject(Router);
```

**d) Add new class fields after `protected submitted = signal(false)`:**

```typescript
protected currentStep = signal(1);
private appointmentId: string | null = null;
private autoSaveTimer: ReturnType<typeof setInterval> | null = null;
```

---

### 2. Update `ngOnInit()`

Replace the existing `ngOnInit` body:

```typescript
ngOnInit(): void {
  this.appointmentId = this.route.snapshot.queryParamMap.get('appointmentId');
  const loaded = this.store.loadDraft();
  if (loaded) {
    this.syncFromStore();
  }
  this.autoSaveTimer = setInterval(() => {
    if (this.store.isDirty()) {
      this.store.saveDraft(this.appointmentId ?? undefined);
    }
  }, 30_000);
}
```

---

### 3. Add `ngOnDestroy()`

```typescript
ngOnDestroy(): void {
  if (this.autoSaveTimer !== null) {
    clearInterval(this.autoSaveTimer);
  }
}
```

---

### 4. Update `saveDraft()`

```typescript
protected saveDraft(): void {
  this.store.saveDraft(this.appointmentId ?? undefined);
}
```

---

### 5. Fix `submit()`

Replace the existing body:

```typescript
protected submit(): void {
  this.submitted.set(true);
  if (!this.chiefComplaint.trim()) {
    this.currentStep.set(1);
    return;
  }
  if (this.appointmentId) {
    void this.store.submitToBackend(this.appointmentId, 'ManualForm').then(() => {
      if (this.store.isSubmitted()) {
        void this.router.navigate(['/intake/summary', this.appointmentId]);
      }
    });
  } else {
    this.store.markSubmitted();
  }
}
```

---

### 6. Add step navigation helpers

```typescript
protected nextStep(): void {
  if (this.currentStep() < 3) this.currentStep.update((s) => s + 1);
}

protected prevStep(): void {
  if (this.currentStep() > 1) this.currentStep.update((s) => s - 1);
}
```

---

### 7. Replace the template

The full new template wraps existing sections in `@if (currentStep() === N)` blocks and adds the step indicator, navigation row, and review panel.

Key structural changes from the existing template (keep the `<div class="form-page">` wrapper and `<app-intake-mode-toggle />` / `<h2>` / `<p>` header intact):

**Replace** everything from `<!-- Progress -->` down to the closing `</div>` of the form-page div with:

```html
<!-- Step indicator -->
<div class="flex gap-1 mb-3" role="navigation" aria-label="Intake form steps">
  @for (n of [1, 2, 3]; track n) {
    <p-tag
      [value]="'Step ' + n"
      [severity]="currentStep() === n ? 'info' : currentStep() > n ? 'success' : 'secondary'"
    />
  }
</div>

<!-- Progress bar -->
<p-progressBar
  [value]="store.progress()"
  [showValue]="true"
  styleClass="mb-4"
  aria-label="Intake form completion"
/>

<!-- ── Step 1: Chief Complaint + Symptoms ── -->
@if (currentStep() === 1) {
  <section class="section">
    <h3 class="section-title">1. Chief Complaint *</h3>
    <textarea
      pTextarea
      [(ngModel)]="chiefComplaint"
      (ngModelChange)="store.patch({ chiefComplaint: $event })"
      placeholder="What brings you in today?"
      rows="3"
      [class.ng-invalid]="submitted() && !chiefComplaint.trim()"
      aria-label="Chief complaint"
      class="w-full"
    ></textarea>
    @if (submitted() && !chiefComplaint.trim()) {
      <small class="text-red-500">Chief complaint is required.</small>
    }
  </section>

  <p-divider />

  <section class="section">
    <h3 class="section-title">2. Symptoms</h3>
    <div class="flex flex-wrap gap-3">
      @for (symptom of commonSymptoms; track symptom) {
        <div class="flex align-items-center gap-2">
          <p-checkbox
            [(ngModel)]="selectedSymptoms"
            [value]="symptom"
            (ngModelChange)="store.patch({ symptoms: $event })"
            [inputId]="'sym-' + symptom"
          />
          <label [for]="'sym-' + symptom">{{ symptom }}</label>
        </div>
      }
    </div>
  </section>
}

<!-- ── Step 2: Duration / Severity / Medications / Allergies ── -->
@if (currentStep() === 2) {
  <section class="section">
    <h3 class="section-title">3. Duration &amp; Severity</h3>
    <div class="flex flex-column gap-3">
      <input
        pInputText
        [(ngModel)]="duration"
        (ngModelChange)="store.patch({ duration: $event })"
        placeholder="e.g. 3 days, 2 weeks"
        aria-label="Symptom duration"
      />
      <div>
        <div class="block mb-2">
          Severity: <strong>{{ severity }} &mdash; {{ severityLabel() }}</strong>
        </div>
        <p-slider
          [(ngModel)]="severity"
          (ngModelChange)="store.patch({ severity: $event })"
          [min]="1"
          [max]="10"
          [step]="1"
          styleClass="w-full"
          aria-label="Symptom severity 1 to 10"
        />
        <div class="flex justify-content-between text-xs text-color-secondary mt-1">
          <span>1 &mdash; Minimal</span>
          <span>5 &mdash; Moderate</span>
          <span>10 &mdash; Critical</span>
        </div>
      </div>
    </div>
  </section>

  <p-divider />

  <section class="section">
    <h3 class="section-title">4. Current Medications</h3>
    <p-autoComplete
      [(ngModel)]="medications"
      (ngModelChange)="store.patch({ medications: $event })"
      [suggestions]="medicationSuggestions()"
      (completeMethod)="filterMedications($event)"
      [multiple]="true"
      placeholder="Type or select medications…"
      aria-label="Current medications"
      styleClass="w-full"
    />
  </section>

  <p-divider />

  <section class="section">
    <h3 class="section-title">5. Allergies</h3>
    <p-autoComplete
      [(ngModel)]="allergies"
      (ngModelChange)="store.patch({ allergies: $event })"
      [suggestions]="allergySuggestions()"
      (completeMethod)="filterAllergies($event)"
      [multiple]="true"
      placeholder="Type or select allergies…"
      aria-label="Known allergies"
      styleClass="w-full"
    />
  </section>
}

<!-- ── Step 3: Medical History + Review Panel ── -->
@if (currentStep() === 3) {
  <section class="section">
    <h3 class="section-title">6. Relevant Medical History</h3>
    <textarea
      pTextarea
      [(ngModel)]="medicalHistory"
      (ngModelChange)="store.patch({ medicalHistory: $event })"
      placeholder="Previous diagnoses, surgeries, chronic conditions…"
      rows="3"
      aria-label="Medical history"
      class="w-full"
    ></textarea>
  </section>

  <p-divider />

  <!-- Review panel -->
  <div class="review-panel" role="region" aria-label="Intake summary review">
    <h3 class="section-title">Review Your Intake</h3>
    <dl class="review-list">
      <dt>Chief Complaint</dt>
      <dd>{{ chiefComplaint || '—' }}</dd>
      <dt>Symptoms</dt>
      <dd>{{ selectedSymptoms.length ? selectedSymptoms.join(', ') : '—' }}</dd>
      <dt>Duration</dt>
      <dd>{{ duration || '—' }}</dd>
      <dt>Severity</dt>
      <dd>{{ severity }} / 10</dd>
      <dt>Medications</dt>
      <dd>{{ medications.length ? medications.join(', ') : 'None reported' }}</dd>
      <dt>Allergies</dt>
      <dd>{{ allergies.length ? allergies.join(', ') : 'None reported' }}</dd>
      <dt>Medical History</dt>
      <dd>{{ medicalHistory || '—' }}</dd>
    </dl>
    <div class="flex gap-2 mt-3">
      <p-button
        label="Edit"
        severity="secondary"
        icon="pi pi-pencil"
        [outlined]="true"
        (onClick)="currentStep.set(1)"
        aria-label="Go back and edit intake form"
      />
      <p-button
        label="Confirm &amp; Submit"
        icon="pi pi-check"
        (onClick)="submit()"
        aria-label="Confirm and submit intake form"
      />
    </div>
  </div>
}

<!-- ── Navigation row (all steps) ── -->
<div class="flex justify-content-between align-items-center mt-4">
  <p-button
    label="Save &amp; Continue Later"
    icon="pi pi-save"
    severity="secondary"
    [outlined]="true"
    (onClick)="saveDraft()"
    aria-label="Save intake draft and continue later"
  />
  <div class="flex gap-2">
    @if (currentStep() > 1) {
      <p-button
        label="Back"
        icon="pi pi-arrow-left"
        severity="secondary"
        (onClick)="prevStep()"
        aria-label="Go to previous step"
      />
    }
    @if (currentStep() < 3) {
      <p-button
        label="Next"
        icon="pi pi-arrow-right"
        iconPos="right"
        (onClick)="nextStep()"
        aria-label="Go to next step"
      />
    }
  </div>
</div>
```

---

### 8. Update component `styles`

Replace the existing styles array content:

```css
.form-page {
  max-width: 720px;
  margin: 0 auto;
  padding: 1.5rem 1rem;
}
.section {
  margin-bottom: 0.5rem;
}
.section-title {
  font-size: 1rem;
  font-weight: 600;
  margin-bottom: 0.75rem;
  color: var(--text-color);
}
.review-panel {
  background: var(--surface-ground);
  border: 1px solid var(--surface-border);
  border-radius: 8px;
  padding: 1.25rem;
  margin-bottom: 0.5rem;
}
.review-list {
  display: grid;
  grid-template-columns: auto 1fr;
  gap: 0.375rem 1rem;
  font-size: 0.9375rem;
}
.review-list dt {
  font-weight: 600;
  color: var(--text-color-secondary);
  white-space: nowrap;
}
.review-list dd {
  margin: 0;
  word-break: break-word;
}
@media (max-width: 640px) {
  .form-page {
    padding: 0.5rem;
    max-width: 100%;
  }
  .review-list {
    grid-template-columns: 1fr;
  }
  .review-list dt {
    margin-top: 0.5rem;
  }
}
```

---

### 9. Update unit tests — `intake-landing.component.spec.ts`

Open `src/health-platform-ui/src/app/features/intake/intake-landing/intake-landing.component.spec.ts`.

The existing `it('should create', ...)` must still pass. Add 2 more tests after it:

```typescript
it('should initialise at step 1', () => {
  fixture.detectChanges();
  expect(fixture.componentInstance['currentStep']()).toBe(1);
});

it('should advance to step 2 via nextStep()', () => {
  fixture.detectChanges();
  fixture.componentInstance['nextStep']();
  expect(fixture.componentInstance['currentStep']()).toBe(2);
});
```

No additional providers are needed — the existing `beforeEach` already has all required providers.

---

## Verification

```bash
cd src/health-platform-ui
npx ng test --no-watch
```

Expected: all existing tests pass + 2 new — **35/35** total (33 from Task 001 + 2 here).

Lint check:

```bash
npx ng lint
```

Expected: `All files pass linting.`

---

## Files Modified

| File | Change |
|------|--------|
| `src/health-platform-ui/src/app/features/intake/intake-landing/intake-landing.component.ts` | Multi-step wizard, auto-save, review panel, submit fix, mobile CSS |
| `src/health-platform-ui/src/app/features/intake/intake-landing/intake-landing.component.spec.ts` | Add 2 tests for step initialisation and nextStep() |
