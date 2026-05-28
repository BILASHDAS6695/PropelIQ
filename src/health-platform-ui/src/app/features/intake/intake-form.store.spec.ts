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
