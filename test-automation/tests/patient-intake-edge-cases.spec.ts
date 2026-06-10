import { test, expect } from '@playwright/test';
import {
  IntakeLandingPage,
  AIConversationalIntakePage,
  ManualFormIntakePage,
  IntakeSummaryPage,
} from '../pages';

test.describe('Patient Intake - Edge Cases', () => {
  test.beforeEach(async ({ page }) => {
    // Assume patient is already authenticated
    await page.goto('/login');
    await page.getByTestId('email-input').fill('patient@example.com');
    await page.getByTestId('password-input').fill('PatientPass123!');
    await page.getByTestId('login-button').click();
    await expect(page.getByTestId('patient-dashboard')).toBeVisible();
  });

  test('TW-INTAKE-005: Switch From Manual to AI Mode', async ({ page }) => {
    const intakeLanding = new IntakeLandingPage(page);
    const manualIntake = new ManualFormIntakePage(page);
    const aiIntake = new AIConversationalIntakePage(page);

    // Start manual form mode
    await intakeLanding.goto();
    await intakeLanding.selectManualMode();

    // Partially fill form
    await manualIntake.fillMedicalHistory('Hypertension');
    await manualIntake.fillAllergies('Latex');

    // Switch to AI mode
    await manualIntake.switchToAI();

    // Verify AI interface displayed
    await expect(aiIntake.chatInterface).toBeVisible();

    // Verify AI acknowledges existing data
    await expect(aiIntake.aiResponse).toContainText('I see you\'ve already provided some information');

    // AI asks about remaining fields
    await aiIntake.waitForQuestion('current medications');

    // Complete via AI
    await aiIntake.sendMessage('Amlodipine 5mg daily');

    // Wait for summary
    await expect(aiIntake.dataSummaryReview).toBeVisible();

    // Verify combined data in final summary
    await aiIntake.verifySummaryData({
      conditions: 'Hypertension',
      allergies: 'Latex',
      medications: 'Amlodipine',
    });
  });

  test('TW-INTAKE-006: Multiple Edit Cycles', async ({ page }) => {
    const intakeSummary = new IntakeSummaryPage(page);
    const manualIntake = new ManualFormIntakePage(page);

    // Prerequisite: Patient has completed intake
    await intakeSummary.goto();

    // First edit - add medication
    await intakeSummary.editIntake();
    await manualIntake.fillCurrentMedications('Existing Medication, New Medication X');
    await intakeSummary.saveChanges();

    // Verify first edit saved
    await intakeSummary.verifyDisplayedData({
      medications: 'New Medication X',
    });

    // Second edit - update allergy
    await intakeSummary.editIntake();
    await manualIntake.fillAllergies('Penicillin, Sulfa drugs');
    await intakeSummary.saveChanges();

    // Verify second edit saved
    await intakeSummary.verifyDisplayedData({
      allergies: 'Sulfa drugs',
    });

    // Third edit - correct symptom
    await intakeSummary.editIntake();
    await manualIntake.fillCurrentSymptoms('Corrected symptom description');
    await intakeSummary.saveChanges();

    // Verify all edits persisted via API
    const response = await page.request.get('/api/intake', {
      headers: {
        'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
      },
    });

    expect(response.ok()).toBeTruthy();
    const intakeData = await response.json();
    expect(intakeData.medications).toContain('New Medication X');
    expect(intakeData.allergies).toBe('Penicillin, Sulfa drugs');
    expect(intakeData.symptoms).toBe('Corrected symptom description');
  });
});
