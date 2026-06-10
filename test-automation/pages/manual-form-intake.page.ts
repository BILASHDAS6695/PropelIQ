import { type Locator, type Page, expect } from '@playwright/test';

/**
 * Page Object Model for Manual Form Intake
 * Handles traditional form-based patient intake
 */
export class ManualFormIntakePage {
  readonly page: Page;
  readonly medicalHistoryInput: Locator;
  readonly currentMedicationsInput: Locator;
  readonly allergiesInput: Locator;
  readonly currentSymptomsInput: Locator;
  readonly submitButton: Locator;
  readonly switchToAIButton: Locator;
  readonly validationErrors: Locator;
  readonly successMessage: Locator;
  readonly manualFormIntake: Locator;
  readonly fieldErrorMedications: Locator;
  readonly fieldErrorAllergies: Locator;

  constructor(page: Page) {
    this.page = page;
    this.medicalHistoryInput = page.getByTestId('medical-history');
    this.currentMedicationsInput = page.getByTestId('current-medications');
    this.allergiesInput = page.getByTestId('allergies');
    this.currentSymptomsInput = page.getByTestId('current-symptoms');
    this.submitButton = page.getByTestId('submit-intake');
    this.switchToAIButton = page.getByTestId('switch-to-ai');
    this.validationErrors = page.getByTestId('validation-errors');
    this.successMessage = page.getByTestId('success-message');
    this.manualFormIntake = page.getByTestId('manual-form-intake');
    this.fieldErrorMedications = page.getByTestId('field-error-medications');
    this.fieldErrorAllergies = page.getByTestId('field-error-allergies');
  }

  /**
   * Fill medical history field
   */
  async fillMedicalHistory(history: string): Promise<void> {
    await this.medicalHistoryInput.fill(history);
  }

  /**
   * Fill current medications field
   */
  async fillCurrentMedications(medications: string): Promise<void> {
    await this.currentMedicationsInput.fill(medications);
  }

  /**
   * Fill allergies field
   */
  async fillAllergies(allergies: string): Promise<void> {
    await this.allergiesInput.fill(allergies);
  }

  /**
   * Fill current symptoms field
   */
  async fillCurrentSymptoms(symptoms: string): Promise<void> {
    await this.currentSymptomsInput.fill(symptoms);
  }

  /**
   * Check a chronic condition checkbox
   */
  async checkCondition(conditionId: string): Promise<void> {
    await this.page.getByTestId(`condition-${conditionId}`).check();
  }

  /**
   * Fill complete intake form
   */
  async fillCompleteForm(data: {
    medicalHistory: string;
    medications: string;
    allergies: string;
    symptoms: string;
    conditions?: string[];
  }): Promise<void> {
    await this.fillMedicalHistory(data.medicalHistory);
    await this.fillCurrentMedications(data.medications);
    await this.fillAllergies(data.allergies);
    await this.fillCurrentSymptoms(data.symptoms);

    if (data.conditions) {
      for (const condition of data.conditions) {
        await this.checkCondition(condition);
      }
    }
  }

  /**
   * Submit the intake form
   */
  async submit(): Promise<void> {
    await this.submitButton.click();
  }

  /**
   * Switch to AI conversational mode
   */
  async switchToAI(): Promise<void> {
    await this.switchToAIButton.click();
  }

  /**
   * Verify success message
   */
  async verifySuccess(): Promise<void> {
    await expect(this.successMessage).toContainText('Intake submitted successfully');
  }

  /**
   * Verify validation errors are displayed
   */
  async verifyValidationErrors(): Promise<void> {
    await expect(this.validationErrors).toBeVisible();
  }

  /**
   * Verify field has specific value
   */
  async verifyFieldValue(field: 'medicalHistory' | 'medications' | 'allergies' | 'symptoms', value: string): Promise<void> {
    const fieldMap = {
      medicalHistory: this.medicalHistoryInput,
      medications: this.currentMedicationsInput,
      allergies: this.allergiesInput,
      symptoms: this.currentSymptomsInput,
    };
    
    await expect(fieldMap[field]).toHaveValue(value);
  }
}
