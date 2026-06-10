import { test, expect } from '@playwright/test';
import {
  LoginPage,
  AppointmentSearchPage,
  PatientDashboardPage,
} from '../pages';

test.describe('Appointment Booking & Management - Edge Cases', () => {

  test('TW-APPT-006: Concurrent Booking Attempts (Race Condition)', async ({ browser }) => {
    // Create two separate browser contexts for parallel booking
    const contextA = await browser.newContext();
    const contextB = await browser.newContext();

    const pageA = await contextA.newPage();
    const pageB = await contextB.newPage();

    const loginA = new LoginPage(pageA);
    const loginB = new LoginPage(pageB);
    const searchA = new AppointmentSearchPage(pageA);
    const searchB = new AppointmentSearchPage(pageB);

    // Both patients login
    await loginA.goto();
    await loginA.login('patient-a@example.com', 'PatientPass123!');

    await loginB.goto();
    await loginB.login('patient-b@example.com', 'PatientPass123!');

    // Both navigate to booking and select same slot
    await Promise.all([
      (async () => {
        await searchA.goto();
        await searchA.searchAppointments('Dr. Smith', '2026-06-15');
        await searchA.selectSlot('10:00 AM');
        await searchA.confirmBooking();
      })(),
      (async () => {
        await searchB.goto();
        await searchB.searchAppointments('Dr. Smith', '2026-06-15');
        await searchB.selectSlot('10:00 AM');
        await searchB.confirmBooking();
      })(),
    ]);

    // Wait for processing
    await pageA.waitForTimeout(1000);

    // Verify only one booking succeeded
    const response = await pageA.request.get('/api/appointments?slot=10:00 AM&date=2026-06-15&provider=Dr. Smith');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data.count).toBe(1);

    // One page should show error
    const errorA = pageA.getByTestId('error-message');
    const errorB = pageB.getByTestId('error-message');

    const errorAVisible = await errorA.isVisible().catch(() => false);
    const errorBVisible = await errorB.isVisible().catch(() => false);

    // Exactly one should have error
    expect(errorAVisible || errorBVisible).toBe(true);

    if (errorAVisible) {
      await expect(errorA).toContainText('This slot is no longer available');
    }
    if (errorBVisible) {
      await expect(errorB).toContainText('This slot is no longer available');
    }

    // Cleanup
    await contextA.close();
    await contextB.close();
  });

  test('TW-APPT-007: Multiple Patients Prefer Same Slot - FIFO Priority', async ({ page, context }) => {
    const loginPage = new LoginPage(page);
    const dashboardPage = new PatientDashboardPage(page);

    // Patient A and Patient B both have preferences for 09:00 AM
    // Patient A registered earlier (2026-06-10 10:00:00)
    // Patient B registered later (2026-06-10 12:30:00)

    // Patient C cancels 09:00 AM slot
    const patientCPage = await context.newPage();
    const loginC = new LoginPage(patientCPage);
    const dashboardC = new PatientDashboardPage(patientCPage);

    await loginC.goto();
    await loginC.login('patient-c@example.com', 'PatientPass123!');
    await dashboardC.goto();
    await dashboardC.cancelAppointment('09:00 AM');

    // Wait for swap processing
    await page.waitForTimeout(2000);

    // Verify Patient A got the slot (earliest registrant)
    await loginPage.goto();
    await loginPage.login('patient-a@example.com', 'PatientPass123!');

    const responseA = await page.request.get('/api/appointments', {
      headers: {
        'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
      },
    });
    expect(responseA.ok()).toBeTruthy();
    const appointmentsA = await responseA.json();
    expect(appointmentsA[0].time).toBe('09:00 AM');

    // Verify Patient B still has original time but preference is active
    await loginPage.goto();
    await loginPage.login('patient-b@example.com', 'PatientPass123!');

    const responseB = await page.request.get('/api/appointments', {
      headers: {
        'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
      },
    });
    expect(responseB.ok()).toBeTruthy();
    const appointmentsB = await responseB.json();
    expect(appointmentsB[0].time).not.toBe('09:00 AM');
    expect(appointmentsB[0].preferred_slot).toBe('09:00 AM');

    // Cleanup
    await patientCPage.close();
  });
});
