import { test, expect } from '@playwright/test';
import {
  IntakeLandingPage,
  AIConversationalIntakePage,
  ManualFormIntakePage,
} from '../pages';

test.describe('Patient Intake - Error Scenarios', () => {
  test.beforeEach(async ({ page }) => {
    // Assume patient is already authenticated
    await page.goto('/login');
    await page.getByTestId('email-input').fill('patient@example.com');
    await page.getByTestId('password-input').fill('PatientPass123!');
    await page.getByTestId('login-button').click();
    await expect(page.getByTestId('patient-dashboard')).toBeVisible();
  });

  test('TW-INTAKE-007: Submit Manual Form With Missing Required Fields', async ({ page }) => {
    const intakeLanding = new IntakeLandingPage(page);
    const manualIntake = new ManualFormIntakePage(page);

    // Navigate to manual form
    await intakeLanding.goto();
    await intakeLanding.selectManualMode();

    // Fill only partial data (missing required fields)
    await manualIntake.fillMedicalHistory('Diabetes');
    // Intentionally skip medications and allergies

    // Attempt to submit incomplete form
    await manualIntake.submit();

    // Verify validation errors displayed
    await manualIntake.verifyValidationErrors();

    await expect(manualIntake.fieldErrorMedications).toContainText('Current medications is required');
    await expect(manualIntake.fieldErrorAllergies).toContainText('Allergies information is required');

    // Verify form not submitted (still on intake page)
    await expect(page).toHaveURL(/.*intake/);
  });

  test('TW-INTAKE-008: AI Parsing Ambiguous Response', async ({ page }) => {
    const intakeLanding = new IntakeLandingPage(page);
    const aiIntake = new AIConversationalIntakePage(page);

    // Start AI intake
    await intakeLanding.goto();
    await intakeLanding.selectAIMode();

    // Wait for greeting
    await aiIntake.waitForGreeting();

    // AI asks about medications
    await aiIntake.waitForQuestion('medications');

    // Provide ambiguous response
    await aiIntake.sendMessage('I take the blue pill and the white one');

    // Verify AI requests clarification
    await expect(aiIntake.aiResponse).toContainText('Could you provide the medication names');

    // Provide clearer response
    await aiIntake.sendMessage('Metformin and Lisinopril');

    // Verify medications now captured
    await aiIntake.verifyCapturedData('Metformin');
  });
});
