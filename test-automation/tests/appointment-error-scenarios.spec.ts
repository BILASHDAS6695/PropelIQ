import { test, expect } from '@playwright/test';
import {
  LoginPage,
  AppointmentSearchPage,
} from '../pages';

test.describe('Appointment Booking & Management - Error Scenarios', () => {

  test('TW-APPT-008: Patient Attempts Self-Check-In (Forbidden)', async ({ page }) => {
    const loginPage = new LoginPage(page);

    // Precondition: Login as patient with upcoming appointment
    await loginPage.goto();
    await loginPage.login('patient@example.com', 'PatientPass123!');

    // Navigate to appointments
    await page.goto('/appointments');

    // Verify no check-in button displayed in UI
    const checkInButton = page.getByTestId('check-in-button');
    await expect(checkInButton).toBeHidden();

    // Verify no QR code option
    const qrCheckIn = page.getByTestId('qr-check-in');
    await expect(qrCheckIn).toBeHidden();

    // Get appointment ID for API test
    const appointmentsResponse = await page.request.get('/api/appointments', {
      headers: {
        'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
      },
    });
    expect(appointmentsResponse.ok()).toBeTruthy();
    const appointments = await appointmentsResponse.json();
    const appointmentId = appointments[0]?.id;

    if (appointmentId) {
      // Attempt direct API call to check-in
      const response = await page.request.post(`/api/appointments/${appointmentId}/check-in`, {
        headers: {
          'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
        },
      });

      // Verify 403 Forbidden
      expect(response.status()).toBe(403);

      const errorData = await response.json();
      expect(errorData.error).toContain('Patients cannot self-check-in');
    }
  });

  test('TW-APPT-009: Booking With No Available Slots', async ({ page }) => {
    const loginPage = new LoginPage(page);
    const appointmentSearch = new AppointmentSearchPage(page);

    // Precondition: Login as patient
    await loginPage.goto();
    await loginPage.login('patient@example.com', 'PatientPass123!');

    // Search for fully booked provider/date
    await appointmentSearch.goto();
    await appointmentSearch.searchAppointments('Dr. Smith', '2026-06-15');

    // Verify no available slots message
    const noSlotsMessage = page.getByTestId('no-slots-message');
    await expect(noSlotsMessage).toBeVisible();
    await expect(noSlotsMessage).toContainText('No appointments available');

    // Verify waitlist option displayed
    const joinWaitlist = page.getByTestId('join-waitlist');
    await expect(joinWaitlist).toBeVisible();
  });
});
