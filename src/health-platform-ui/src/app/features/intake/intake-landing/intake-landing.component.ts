import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DividerModule } from 'primeng/divider';
import { ProgressBarModule } from 'primeng/progressbar';
import { SliderModule } from 'primeng/slider';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { InputTextModule } from 'primeng/inputtext';
import { ALLERGY_SUGGESTIONS, IntakeFormStore, MEDICATION_SUGGESTIONS } from '../intake-form.store';
import { IntakeModeToggleComponent } from '../intake-mode-toggle/intake-mode-toggle.component';

const COMMON_SYMPTOMS = [
  'Headache',
  'Fever',
  'Cough',
  'Shortness of breath',
  'Chest pain',
  'Nausea',
  'Vomiting',
  'Diarrhea',
  'Fatigue',
  'Dizziness',
  'Back pain',
  'Joint pain',
  'Rash',
  'Swelling',
  'Other',
];

const SEVERITY_LABELS: Record<number, string> = {
  1: 'Minimal',
  2: 'Minimal',
  3: 'Mild',
  4: 'Mild',
  5: 'Moderate',
  6: 'Moderate',
  7: 'Severe',
  8: 'Severe',
  9: 'Critical',
  10: 'Critical',
};

@Component({
  selector: 'app-intake-landing',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    FormsModule,
    AutoCompleteModule,
    ButtonModule,
    CheckboxModule,
    DividerModule,
    ProgressBarModule,
    SliderModule,
    TagModule,
    TextareaModule,
    InputTextModule,
    IntakeModeToggleComponent,
  ],
  template: `
    <div class="form-page">
      <app-intake-mode-toggle />

      <h2 class="text-xl font-semibold mb-1">Pre-Visit Intake Form</h2>
      <p class="text-color-secondary text-sm mb-3">
        Fields marked * are required. Your information is kept private.
      </p>

      <!-- Progress -->
      <p-progressBar
        [value]="store.progress()"
        [showValue]="true"
        styleClass="mb-4"
        aria-label="Intake form completion"
      />

      <!-- 1. Chief Complaint -->
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

      <!-- 2. Symptoms -->
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

      <p-divider />

      <!-- 3. Duration & Severity -->
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

      <!-- 4. Medications -->
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

      <!-- 5. Allergies -->
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

      <p-divider />

      <!-- 6. Medical History -->
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

      <!-- Actions -->
      <div class="flex gap-2 mt-2">
        <p-button
          label="Save Draft"
          icon="pi pi-save"
          severity="secondary"
          [outlined]="true"
          (onClick)="saveDraft()"
          aria-label="Save intake draft"
        />
        <p-button
          label="Submit Intake"
          icon="pi pi-check"
          (onClick)="submit()"
          aria-label="Submit intake form"
        />
      </div>
    </div>
  `,
  styles: [
    `
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
    `,
  ],
})
export class IntakeLandingComponent implements OnInit {
  protected readonly store = inject(IntakeFormStore);
  protected readonly commonSymptoms = COMMON_SYMPTOMS;

  protected chiefComplaint = '';
  protected selectedSymptoms: string[] = [];
  protected duration = '';
  protected severity = 5;
  protected medications: string[] = [];
  protected allergies: string[] = [];
  protected medicalHistory = '';

  protected medicationSuggestions = signal<string[]>([]);
  protected allergySuggestions = signal<string[]>([]);
  protected submitted = signal(false);

  ngOnInit(): void {
    const loaded = this.store.loadDraft();
    if (loaded) {
      this.syncFromStore();
    }
  }

  protected severityLabel(): string {
    return SEVERITY_LABELS[this.severity] ?? 'Moderate';
  }

  protected filterMedications(event: { query: string }): void {
    const q = event.query.toLowerCase();
    this.medicationSuggestions.set(
      MEDICATION_SUGGESTIONS.filter((m) => m.toLowerCase().includes(q)),
    );
  }

  protected filterAllergies(event: { query: string }): void {
    const q = event.query.toLowerCase();
    this.allergySuggestions.set(ALLERGY_SUGGESTIONS.filter((a) => a.toLowerCase().includes(q)));
  }

  protected saveDraft(): void {
    this.store.saveDraft();
  }

  protected submit(): void {
    this.submitted.set(true);
    if (!this.chiefComplaint.trim()) return;
    this.store.markSubmitted();
  }

  private syncFromStore(): void {
    const f = this.store.form();
    this.chiefComplaint = f.chiefComplaint;
    this.selectedSymptoms = [...f.symptoms];
    this.duration = f.duration;
    this.severity = f.severity;
    this.medications = [...f.medications];
    this.allergies = [...f.allergies];
    this.medicalHistory = f.medicalHistory;
  }
}
