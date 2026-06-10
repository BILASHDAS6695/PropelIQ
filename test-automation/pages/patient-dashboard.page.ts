import { type Locator, type Page, expect } from '@playwright/test';

/**
 * Page Object Model for Patient Dashboard Page
 * Handles patient's view of their appointments
 */
export class PatientDashboardPage {
  readonly page: Page;
  readonly upcomingAppointments: Locator;
  readonly cancelButton: Locator;
  readonly confirmCancelButton: Locator;

  constructor(page: Page) {
    this.page = page;
    this.upcomingAppointments = page.getByTestId('upcoming-appointments');
    this.cancelButton = page.getByTestId('cancel-appointment');
    this.confirmCancelButton = page.getByTestId('confirm-cancel');
  }

  /**
   * Navigate to patient dashboard
   */
  async goto(): Promise<void> {
    await this.page.goto('/appointments');
  }

  /**
   * Get appointment card by time slot
   */
  getAppointmentCard(timeSlot: string): Locator {
    const slotId = timeSlot.replace(/[: ]/g, '-');
    return this.page.getByTestId(`appointment-${slotId}`);
  }

  /**
   * Cancel an appointment
   */
  async cancelAppointment(timeSlot: string): Promise<void> {
    const slotId = timeSlot.replace(/[: ]/g, '-');
    await this.page.getByTestId(`cancel-appointment-${slotId}`).click();
    await this.confirmCancelButton.click();
  }

  /**
   * Verify appointment is displayed
   */
  async verifyAppointmentDisplayed(data: {
    provider?: string;
    date?: string;
    time?: string;
  }): Promise<void> {
    if (data.provider) {
      await expect(this.upcomingAppointments).toContainText(data.provider);
    }
    if (data.date) {
      await expect(this.upcomingAppointments).toContainText(data.date);
    }
    if (data.time) {
      await expect(this.upcomingAppointments).toContainText(data.time);
    }
  }
}
