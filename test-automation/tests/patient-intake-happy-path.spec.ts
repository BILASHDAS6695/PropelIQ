import { test, expect } from '@playwright/test';
import {
  IntakeLandingPage,
  AIConversationalIntakePage,
  ManualFormIntakePage,
  IntakeSummaryPage,
} from '../pages';

test.describe('Patient Intake - Happy Path', () => {
  test.beforeEach(async ({ page }) => {
    // Assume patient is already authenticated
    await page.goto('/login');
    await page.getByTestId('email-input').fill('patient@example.com');
    await page.getByTestId('password-input').fill('PatientPass123!');
    await page.getByTestId('login-button').click();
    await expect(page.getByTestId('patient-dashboard')).toBeVisible();
  });

  test('TW-INTAKE-001: Complete AI Conversational Intake', async ({ page }) => {
    const intakeLanding = new IntakeLandingPage(page);
    const aiIntake = new AIConversationalIntakePage(page);

    // Navigate to intake
    await intakeLanding.goto();

    // Select AI conversational mode
    await intakeLanding.selectAIMode();

    // Wait for AI greeting
    await aiIntake.waitForGreeting();

    // AI asks about chronic conditions
    await aiIntake.waitForQuestion('chronic conditions');

    // Patient responds with conditions
    await aiIntake.sendMessage('I have type 2 diabetes and hypertension');

    // Verify AI parses response
    await aiIntake.verifyCapturedData('Type 2 Diabetes');

    // AI asks about current medications
    await aiIntake.waitForQuestion('medications');

    // Patient responds with medications
    await aiIntake.sendMessage('Metformin 500mg twice daily and Lisinopril 10mg once daily');

    // Verify medications captured
    await aiIntake.verifyCapturedData('Metformin 500mg');
    await aiIntake.verifyCapturedData('Lisinopril 10mg');

    // AI asks about allergies
    await aiIntake.waitForQuestion('allergies');

    // Patient responds about allergies
    await aiIntake.sendMessage('I\'m allergic to penicillin');

    // AI asks about current symptoms
    await aiIntake.waitForQuestion('symptoms');

    // Patient describes symptoms
    await aiIntake.sendMessage('I have occasional headaches and mild dizziness');

    // AI presents summary for review
    await expect(aiIntake.dataSummaryReview).toBeVisible();

    // Verify all captured data in summary
    await aiIntake.verifySummaryData({
      conditions: 'Type 2 Diabetes',
      medications: 'Metformin',
      allergies: 'Penicillin',
      symptoms: 'headaches',
    });

    // Confirm intake data
    await aiIntake.confirmIntake();

    // Verify success message
    await aiIntake.verifySuccess();
  });

  test('TW-INTAKE-002: Complete Manual Form Intake', async ({ page }) => {
    const intakeLanding = new IntakeLandingPage(page);
    const manualIntake = new ManualFormIntakePage(page);

    // Navigate to intake
    await intakeLanding.goto();

    // Select manual form mode
    await intakeLanding.selectManualMode();

    // Fill complete intake form
    await manualIntake.fillCompleteForm({
      medicalHistory: 'Type 2 Diabetes (diagnosed 2015), Hypertension (diagnosed 2018)',
      medications: 'Metformin 500mg twice daily, Lisinopril 10mg once daily',
      allergies: 'Penicillin',
      symptoms: 'Occasional headaches, mild dizziness',
      conditions: ['diabetes', 'hypertension'],
    });

    // Submit form
    await manualIntake.submit();

    // Verify success message
    await manualIntake.verifySuccess();
  });

  test('TW-INTAKE-003: Switch From AI to Manual Mode Mid-Process', async ({ page }) => {
    const intakeLanding = new IntakeLandingPage(page);
    const aiIntake = new AIConversationalIntakePage(page);
    const manualIntake = new ManualFormIntakePage(page);

    // Start AI conversational mode
    await intakeLanding.goto();
    await intakeLanding.selectAIMode();

    // Wait for AI greeting
    await aiIntake.waitForGreeting();

    // Answer first question (conditions)
    await aiIntake.waitForQuestion('chronic conditions');
    await aiIntake.sendMessage('I have diabetes');

    // Answer second question (medications)
    await aiIntake.waitForQuestion('medications');
    await aiIntake.sendMessage('Metformin 500mg');

    // Verify partial data captured
    await aiIntake.verifyCapturedData('Diabetes');
    await aiIntake.verifyCapturedData('Metformin');

    // Switch to manual mode
    await aiIntake.switchToManual();

    // Verify mode switched
    await expect(manualIntake.manualFormIntake).toBeVisible();

    // Verify previously captured data pre-populated
    await manualIntake.verifyFieldValue('medicalHistory', 'Diabetes');
    await manualIntake.verifyFieldValue('medications', 'Metformin 500mg');

    // Complete remaining fields manually
    await manualIntake.fillAllergies('None');
    await manualIntake.fillCurrentSymptoms('Fatigue');

    // Submit form
    await manualIntake.submit();

    // Verify success message
    await expect(manualIntake.successMessage).toBeVisible();

    // Verify all data saved via API
    const response = await page.request.get('/api/intake', {
      headers: {
        'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
      },
    });

    expect(response.ok()).toBeTruthy();
    const intakeData = await response.json();
    expect(intakeData.medical_history).toContain('Diabetes');
    expect(intakeData.medications).toContain('Metformin 500mg');
    expect(intakeData.allergies).toBe('None');
    expect(intakeData.symptoms).toBe('Fatigue');
  });

  test('TW-INTAKE-004: Patient Edits Submitted Intake', async ({ page }) => {
    const intakeSummary = new IntakeSummaryPage(page);
    const manualIntake = new ManualFormIntakePage(page);

    // Navigate to intake summary
    await intakeSummary.goto();

    // View current intake data
    await intakeSummary.verifyDisplayedData({
      medications: 'Metformin 500mg',
    });

    // Click edit button
    await intakeSummary.editIntake();

    // Modify medication dosage
    await manualIntake.fillCurrentMedications('Metformin 1000mg twice daily, Aspirin 81mg daily');

    // Save changes
    await intakeSummary.saveChanges();

    // Verify update confirmation
    await intakeSummary.verifyUpdateSuccess();

    // Verify updated data displayed
    await intakeSummary.verifyDisplayedData({
      medications: 'Metformin 1000mg',
    });
    await intakeSummary.verifyDisplayedData({
      medications: 'Aspirin 81mg',
    });

    // Verify no staff intervention required (check audit log via API)
    const auditResponse = await page.request.get('/api/audit-logs?action=UPDATE_INTAKE', {
      headers: {
        'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
      },
    });

    expect(auditResponse.ok()).toBeTruthy();
    const auditLogs = await auditResponse.json();
    expect(auditLogs[0].performer_role).toBe('Patient');
  });
});
