import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { IntakeFormData, IntakeFormDraft } from '../../core/models/intake.models';
import { ToastService } from '../../shared/services/toast.service';

export const MEDICATION_SUGGESTIONS = [
  'Aspirin',
  'Ibuprofen',
  'Paracetamol',
  'Metformin',
  'Atorvastatin',
  'Lisinopril',
  'Amlodipine',
  'Omeprazole',
  'Simvastatin',
  'Metoprolol',
  'Amoxicillin',
  'Azithromycin',
  'Levothyroxine',
  'Gabapentin',
  'Sertraline',
];

export const ALLERGY_SUGGESTIONS = [
  'Penicillin',
  'Aspirin',
  'Ibuprofen',
  'Sulfa drugs',
  'Codeine',
  'Latex',
  'Peanuts',
  'Tree nuts',
  'Shellfish',
  'Eggs',
  'Milk',
  'Soy',
  'Wheat',
  'Bee stings',
  'Contrast dye',
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
            ? collected['medications']
                .split(',')
                .map((s) => s.trim())
                .filter(Boolean)
            : [],
          allergies: collected['allergies']
            ? collected['allergies']
                .split(',')
                .map((s) => s.trim())
                .filter(Boolean)
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
