import { test, expect } from '@playwright/test';
import {
  RegistrationPage,
  LoginPage,
  AppointmentSearchPage,
  AppointmentConfirmationPage,
  IntakeLandingPage,
  AIConversationalIntakePage,
  DashboardPage,
} from '../pages';

test.describe('E2E: Complete Patient Appointment Lifecycle', () => {
  
  test('Complete Patient Journey - Registration to Attendance', async ({ page, context }) => {
    // Test data
    const patientData = {
      name: 'Emma Johnson',
      email: 'emma.johnson@example.com',
      phone: '555-2468',
      password: 'SecurePass123!',
    };

    const appointmentData = {
      provider: 'Dr. Smith',
      date: '2026-06-20',
      time: '02:00 PM',
      preferredTime: '11:00 AM',
    };

    const intakeData = {
      medicalHistory: 'I have seasonal allergies and occasional migraines',
      medications: 'I take Zyrtec 10mg daily for allergies',
      allergies: 'No drug allergies, only environmental allergies to pollen',
      symptoms: 'Currently experiencing seasonal allergy symptoms - sneezing and itchy eyes',
    };

    let appointmentId: string;

    // ============================================================
    // PHASE 1: Patient Registration & Activation
    // ============================================================
    test.step('Phase 1: Patient Registration & Activation', async () => {
      const registrationPage = new RegistrationPage(page);
      const loginPage = new LoginPage(page);
      const dashboard = new DashboardPage(page);

      // Navigate to registration
      await page.goto('/');
      await page.getByTestId('register-link').click();
      await expect(page).toHaveURL(/.*register/);

      // Complete registration form
      await registrationPage.register({
        name: patientData.name,
        email: patientData.email,
        phone: patientData.phone,
        password: patientData.password,
        confirmPassword: patientData.password,
      });

      // Verify registration success
      await registrationPage.verifySuccess();
      const activationPrompt = page.getByTestId('activation-prompt');
      await expect(activationPrompt).toContainText('Check your email');

      // Simulate email activation (in real test, would check actual email)
      // For automation, we can use API to activate or use a test activation endpoint
      const activationResponse = await page.request.post('/api/test/activate-account', {
        data: { email: patientData.email },
      });
      expect(activationResponse.ok()).toBeTruthy();

      // Login to account
      await loginPage.goto();
      await loginPage.login(patientData.email, patientData.password);

      // Verify patient dashboard access
      await expect(page).toHaveURL(/.*patient\/dashboard/);
      await dashboard.verifyPatientDashboard();
      const welcomeMessage = page.getByTestId('welcome-message');
      await expect(welcomeMessage).toContainText('Welcome, Emma');
    });

    // ============================================================
    // PHASE 2: Appointment Search & Booking
    // ============================================================
    test.step('Phase 2: Appointment Search & Booking', async () => {
      const appointmentSearch = new AppointmentSearchPage(page);

      // Navigate to booking
      await page.getByTestId('book-appointment-link').click();
      await expect(page).toHaveURL(/.*appointments\/book/);

      // Search for available slots
      await appointmentSearch.searchAppointments(
        appointmentData.provider,
        appointmentData.date
      );

      // Verify slots displayed
      await appointmentSearch.verifySlotsDisplayed('09:00 AM');
      await expect(page.getByTestId('slot-09-00-AM')).toBeVisible();
      await expect(page.getByTestId('slot-10-00-AM')).toBeVisible();
      // Verify unavailable slot is not displayed
      await expect(page.getByTestId('slot-11-00-AM')).not.toBeVisible();

      // Select 2:00 PM slot
      await appointmentSearch.selectSlot(appointmentData.time);
      const selectedSlot = page.getByTestId('selected-slot');
      await expect(selectedSlot).toContainText('2:00 PM');
    });

    // ============================================================
    // PHASE 3: Preferred Slot Registration
    // ============================================================
    test.step('Phase 3: Preferred Slot Registration', async () => {
      const appointmentSearch = new AppointmentSearchPage(page);

      // Expand preferred slot options
      await page.getByTestId('preferred-slot-toggle').click();
      await expect(page.getByTestId('preferred-slot-section')).toBeVisible();

      // View all slots including unavailable
      await appointmentSearch.showAllSlots();
      await expect(page.getByTestId('slot-11-00-AM-unavailable')).toBeVisible();

      // Select 11:00 AM as preferred
      await page.getByTestId('slot-11-00-AM-preferred').click();
      const preferredSlotSelected = page.getByTestId('preferred-slot-selected');
      await expect(preferredSlotSelected).toContainText('11:00 AM');

      // Confirm booking with preference
      await appointmentSearch.confirmBooking();
    });

    // ============================================================
    // PHASE 4: Appointment Confirmation & PDF
    // ============================================================
    test.step('Phase 4: Appointment Confirmation & PDF', async () => {
      const confirmation = new AppointmentConfirmationPage(page);

      // Verify confirmation message
      await confirmation.verifyConfirmationMessage('Appointment confirmed for 2:00 PM');

      // Verify appointment details
      await confirmation.verifyAppointmentDetails({
        provider: 'Dr. Smith',
        date: 'June 20, 2026',
        time: '2:00 PM',
      });

      // Verify preferred slot info
      await confirmation.verifyPreferredSlotInfo('11:00 AM');

      // Verify PDF download available
      await confirmation.verifyPDFDownloadAvailable();

      // Download PDF (optional - can verify download)
      const downloadPromise = page.waitForEvent('download');
      await confirmation.downloadPDF();
      const download = await downloadPromise;
      expect(download.suggestedFilename()).toContain('appointment-confirmation.pdf');

      // Get appointment ID for later phases
      const response = await page.request.get('/api/appointments', {
        headers: {
          'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
        },
      });
      expect(response.ok()).toBeTruthy();
      const appointments = await response.json();
      appointmentId = appointments[0].id;
      expect(appointmentId).toBeTruthy();
    });

    // ============================================================
    // PHASE 5: AI-Assisted Intake Completion
    // ============================================================
    test.step('Phase 5: AI-Assisted Intake Completion', async () => {
      const intakeLanding = new IntakeLandingPage(page);
      const aiIntake = new AIConversationalIntakePage(page);

      // Navigate to intake
      await page.getByTestId('complete-intake-link').click();
      await expect(page).toHaveURL(/.*intake/);

      // Select AI mode
      await intakeLanding.selectAIMode();

      // Wait for AI greeting
      await aiIntake.waitForGreeting();

      // Respond to medical history question
      await aiIntake.waitForQuestion('medical history');
      await aiIntake.sendMessage(intakeData.medicalHistory);

      // Respond to medications question
      await aiIntake.waitForQuestion('medications');
      await aiIntake.sendMessage(intakeData.medications);

      // Respond to allergies question
      await aiIntake.waitForQuestion('allergies');
      await aiIntake.sendMessage(intakeData.allergies);

      // Respond to symptoms question
      await aiIntake.waitForQuestion('symptoms');
      await aiIntake.sendMessage(intakeData.symptoms);

      // Review captured data
      await aiIntake.verifySummaryData({
        conditions: 'Seasonal allergies',
        medications: 'Zyrtec',
        allergies: 'Pollen',
        symptoms: 'sneezing',
      });

      // Confirm intake
      await aiIntake.confirmIntake();

      // Verify success
      await aiIntake.verifySuccess();
    });

    // ============================================================
    // PHASE 6: Calendar Integration
    // ============================================================
    test.step('Phase 6: Calendar Integration', async () => {
      // Return to appointment details
      await page.getByTestId('my-appointments').click();
      await page.getByTestId('appointment-june-20').click();

      // Click sync calendar button
      await page.getByTestId('sync-calendar').click();

      // Select Google Calendar
      await page.getByTestId('calendar-provider').selectOption('Google Calendar');

      // For testing, mock the OAuth flow
      // In real scenario, this would open OAuth popup
      await page.route('**/api/calendar/google/authorize', (route) => {
        route.fulfill({
          status: 200,
          body: JSON.stringify({
            success: true,
            message: 'Calendar synced',
          }),
        });
      });

      await page.getByTestId('authorize-google').click();

      // Verify sync success
      const syncSuccess = page.getByTestId('calendar-sync-success');
      await expect(syncSuccess).toContainText('Synced to Google Calendar');
    });

    // ============================================================
    // PHASE 7: Appointment Reminders (Verification)
    // ============================================================
    test.step('Phase 7: Verify Reminder Configuration', async () => {
      // Verify reminders are scheduled via API
      const reminderResponse = await page.request.get(`/api/appointments/${appointmentId}/reminders`, {
        headers: {
          'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
        },
      });

      expect(reminderResponse.ok()).toBeTruthy();
      const reminders = await reminderResponse.json();

      // Verify 24-hour reminder is scheduled
      expect(reminders).toContainEqual(
        expect.objectContaining({
          type: 'email',
          scheduled_time: '2026-06-19T14:00:00',
        })
      );

      expect(reminders).toContainEqual(
        expect.objectContaining({
          type: 'sms',
          scheduled_time: '2026-06-19T14:00:00',
        })
      );

      // Verify 2-hour reminder is scheduled
      expect(reminders).toContainEqual(
        expect.objectContaining({
          type: 'email',
          scheduled_time: '2026-06-20T12:00:00',
        })
      );
    });

    // ============================================================
    // PHASE 8: Appointment Day - Arrival (Staff Perspective)
    // ============================================================
    test.step('Phase 8: Patient Arrival - Staff Marks Attendance', async () => {
      // Open new page for staff session
      const staffPage = await context.newPage();
      const staffLogin = new LoginPage(staffPage);

      // Staff logs in
      await staffLogin.goto();
      await staffLogin.login('staff@clinic.com', 'StaffPass123!');

      // Navigate to schedule
      await staffPage.goto('/staff/schedule');

      // Verify patient appears in schedule
      const scheduleJune20 = staffPage.getByTestId('schedule-june-20');
      await expect(scheduleJune20).toContainText('Emma Johnson - 2:00 PM');

      // Click on patient
      await staffPage.getByTestId('patient-emma-johnson').click();

      // Verify status is Scheduled
      const patientStatus = staffPage.getByTestId('patient-status');
      await expect(patientStatus).toContainText('Scheduled');

      // Mark patient as arrived
      await staffPage.getByTestId('mark-arrived').click();

      // Confirm arrival
      const confirmationDialog = staffPage.getByTestId('confirmation-dialog');
      await expect(confirmationDialog).toContainText('Mark Emma Johnson as arrived?');
      await staffPage.getByTestId('confirm-arrived').click();

      // Verify arrival status updated
      await expect(patientStatus).toContainText('Arrived');

      // Verify arrival time displayed
      const arrivalTime = staffPage.getByTestId('arrival-time');
      await expect(arrivalTime).toBeVisible();

      // Verify via API
      const arrivalResponse = await staffPage.request.get(`/api/appointments/${appointmentId}`, {
        headers: {
          'Authorization': `Bearer ${await staffPage.evaluate(() => localStorage.getItem('auth_token'))}`,
        },
      });

      expect(arrivalResponse.ok()).toBeTruthy();
      const appointmentData = await arrivalResponse.json();
      expect(appointmentData.status).toBe('arrived');
      expect(appointmentData.arrival_timestamp).toBeTruthy();

      // Close staff page
      await staffPage.close();
    });

    // ============================================================
    // CROSS-PHASE VALIDATION
    // ============================================================
    test.step('Cross-Phase Validation', async () => {
      // Verify complete patient record
      const userResponse = await page.request.get(`/api/users?email=${patientData.email}`, {
        headers: {
          'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
        },
      });

      expect(userResponse.ok()).toBeTruthy();
      const userData = await userResponse.json();
      expect(userData.name).toBe(patientData.name);
      expect(userData.email).toBe(patientData.email);
      expect(userData.status).toBe('active');

      // Verify appointment booked
      const appointmentResponse = await page.request.get('/api/appointments', {
        headers: {
          'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
        },
      });

      expect(appointmentResponse.ok()).toBeTruthy();
      const appointments = await appointmentResponse.json();
      expect(appointments.length).toBeGreaterThan(0);
      expect(appointments[0].provider).toBe('Dr. Smith');
      expect(appointments[0].time).toBe('02:00 PM');
      expect(appointments[0].preferred_slot).toBe('11:00 AM');

      // Verify intake completed
      const intakeResponse = await page.request.get('/api/intake', {
        headers: {
          'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
        },
      });

      expect(intakeResponse.ok()).toBeTruthy();
      const intakeData = await intakeResponse.json();
      expect(intakeData.medical_history).toContain('allergies');
      expect(intakeData.medications).toContain('Zyrtec');
      expect(intakeData.allergies).toContain('Pollen');

      // Verify audit trail
      const auditResponse = await page.request.get(`/api/admin/audit-logs?entity_id=${appointmentId}`, {
        headers: {
          'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
        },
      });

      if (auditResponse.ok()) {
        const auditLogs = await auditResponse.json();
        expect(auditLogs.length).toBeGreaterThan(0);
      }
    });
  });
});
