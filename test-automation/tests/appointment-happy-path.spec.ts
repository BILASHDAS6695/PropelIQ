import { test, expect } from '@playwright/test';
import {
  LoginPage,
  AppointmentSearchPage,
  AppointmentConfirmationPage,
  StaffBookingPage,
  WalkInPage,
  PatientDashboardPage,
} from '../pages';

test.describe('Appointment Booking & Management - Happy Path', () => {

  test('TW-APPT-001: Patient Searches and Books Available Slot', async ({ page }) => {
    const loginPage = new LoginPage(page);
    const appointmentSearch = new AppointmentSearchPage(page);
    const confirmation = new AppointmentConfirmationPage(page);

    // Precondition: Login as patient
    await loginPage.goto();
    await loginPage.login('patient@example.com', 'PatientPass123!');

    // Navigate to appointment booking
    await appointmentSearch.goto();

    // Search for appointments
    await appointmentSearch.searchAppointments('Dr. Smith', '2026-06-15');

    // Verify available slots displayed
    await appointmentSearch.verifySlotsDisplayed('09:00 AM');

    // Select 10:00 AM slot
    await appointmentSearch.selectSlot('10:00 AM');

    // Confirm booking
    await appointmentSearch.confirmBooking();

    // Verify confirmation message
    await confirmation.verifyConfirmationMessage('Appointment confirmed');

    // Verify appointment details
    await confirmation.verifyAppointmentDetails({
      provider: 'Dr. Smith',
      date: 'June 15, 2026',
      time: '10:00 AM',
    });

    // Verify PDF download available
    await confirmation.verifyPDFDownloadAvailable();

    // Checkpoint: Verify appointment created via API
    const response = await page.request.get('/api/appointments', {
      headers: {
        'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
      },
    });
    expect(response.ok()).toBeTruthy();
    const appointments = await response.json();
    expect(appointments).toContainEqual(
      expect.objectContaining({
        provider: 'Dr. Smith',
        date: '2026-06-15',
        time: '10:00 AM',
      })
    );

    // Checkpoint: Verify slot is no longer available
    const slotsResponse = await page.request.get('/api/slots?date=2026-06-15&time=10:00 AM&provider=Dr. Smith');
    expect(slotsResponse.ok()).toBeTruthy();
    const slotData = await slotsResponse.json();
    expect(slotData.available).toBe(false);
  });

  test('TW-APPT-002: Patient Books With Preferred Slot Selection', async ({ page }) => {
    const loginPage = new LoginPage(page);
    const appointmentSearch = new AppointmentSearchPage(page);
    const confirmation = new AppointmentConfirmationPage(page);

    // Precondition: Login as patient
    await loginPage.goto();
    await loginPage.login('patient@example.com', 'PatientPass123!');

    // Navigate to booking
    await appointmentSearch.goto();

    // Book appointment with preferred slot
    await appointmentSearch.bookWithPreferredSlot({
      provider: 'Dr. Smith',
      date: '2026-06-15',
      selectedSlot: '02:00 PM',
      preferredSlot: '10:00 AM',
    });

    // Verify booking confirmation shows 2:00 PM
    await confirmation.verifyAppointmentDetails({
      time: '2:00 PM',
    });

    // Verify preferred slot registered
    await confirmation.verifyPreferredSlotInfo('10:00 AM');

    // Checkpoint: Verify preference registered in database
    const response = await page.request.get('/api/appointments', {
      headers: {
        'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
      },
    });
    expect(response.ok()).toBeTruthy();
    const appointments = await response.json();
    const appointment = appointments.find((a: any) => a.time === '02:00 PM');
    expect(appointment.preferred_slot).toBe('10:00 AM');
  });

  test('TW-APPT-003: Staff Books Appointment for Patient', async ({ page }) => {
    const loginPage = new LoginPage(page);
    const staffBooking = new StaffBookingPage(page);

    // Precondition: Login as staff
    await loginPage.goto();
    await loginPage.login('staff@clinic.com', 'StaffPass123!');

    // Navigate to staff booking
    await staffBooking.goto();

    // Book appointment for patient
    await staffBooking.bookForPatient({
      patientName: 'John Doe',
      provider: 'Dr. Smith',
      date: '2026-06-16',
      timeSlot: '11:00 AM',
    });

    // Verify booking confirmation
    await staffBooking.verifyBookingSuccess('John Doe');

    // Checkpoint: Verify appointment linked to correct patient
    const response = await page.request.get('/api/appointments?patient_email=john.doe@example.com', {
      headers: {
        'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
      },
    });
    expect(response.ok()).toBeTruthy();
    const appointments = await response.json();
    expect(appointments).toContainEqual(
      expect.objectContaining({
        provider: 'Dr. Smith',
        date: '2026-06-16',
        time: '11:00 AM',
      })
    );
  });

  test('TW-APPT-004: Staff Registers Walk-In and Marks Arrived', async ({ page }) => {
    const loginPage = new LoginPage(page);
    const walkIn = new WalkInPage(page);

    // Precondition: Login as staff
    await loginPage.goto();
    await loginPage.login('staff@clinic.com', 'StaffPass123!');

    // Navigate to walk-in section
    await walkIn.goto();

    // Register walk-in patient
    await walkIn.registerWalkIn({
      name: 'Jane Smith',
      phone: '555-9876',
      email: 'jane.smith@example.com',
    });

    // Verify walk-in added to queue
    await walkIn.verifyPatientInQueue('Jane Smith');

    // Verify timestamp recorded
    await walkIn.verifyTimestamp();

    // Mark patient as arrived
    await walkIn.selectPatientInQueue('Jane Smith');
    await walkIn.markAsArrived();

    // Verify arrival status updated
    await walkIn.verifyArrivalStatus('Jane Smith', 'Arrived');

    // Checkpoint: Verify walk-in record created
    const response = await page.request.get('/api/appointments?type=walk-in', {
      headers: {
        'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
      },
    });
    expect(response.ok()).toBeTruthy();
    const appointments = await response.json();
    expect(appointments).toContainEqual(
      expect.objectContaining({
        patient_name: 'Jane Smith',
        type: 'walk-in',
        status: 'arrived',
      })
    );
  });

  test('TW-APPT-005: Automatic Preferred Slot Swap', async ({ page, context }) => {
    const loginPageA = new LoginPage(page);
    const dashboardA = new PatientDashboardPage(page);

    // Precondition: Patient A has appointment at 2:00 PM with preferred slot 10:00 AM
    await loginPageA.goto();
    await loginPageA.login('patient-a@example.com', 'PatientPass123!');

    // Verify initial appointment at 2:00 PM
    let appointmentResponse = await page.request.get('/api/appointments', {
      headers: {
        'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
      },
    });
    expect(appointmentResponse.ok()).toBeTruthy();
    let appointments = await appointmentResponse.json();
    let currentAppointment = appointments[0];
    expect(currentAppointment.time).toBe('02:00 PM');

    // Different patient cancels 10:00 AM slot
    const patientBPage = await context.newPage();
    const loginPageB = new LoginPage(patientBPage);
    const dashboardB = new PatientDashboardPage(patientBPage);

    await loginPageB.goto();
    await loginPageB.login('patient-b@example.com', 'PatientPass123!');
    await dashboardB.goto();
    await dashboardB.cancelAppointment('10:00 AM');

    // Wait for swap processing
    await page.waitForTimeout(2000);

    // Verify Patient A appointment updated to 10:00 AM
    appointmentResponse = await page.request.get('/api/appointments', {
      headers: {
        'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
      },
    });
    expect(appointmentResponse.ok()).toBeTruthy();
    appointments = await appointmentResponse.json();
    currentAppointment = appointments[0];
    expect(currentAppointment.time).toBe('10:00 AM');

    // Verify 2:00 PM slot released
    const slotResponse = await page.request.get('/api/slots?date=2026-06-15&time=02:00 PM');
    expect(slotResponse.ok()).toBeTruthy();
    const slotData = await slotResponse.json();
    expect(slotData.available).toBe(true);

    // Cleanup
    await patientBPage.close();
  });
});
