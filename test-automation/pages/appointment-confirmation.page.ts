import { type Locator, type Page, expect } from '@playwright/test';

/**
 * Page Object Model for Appointment Confirmation Page
 * Handles appointment confirmation details
 */
export class AppointmentConfirmationPage {
  readonly page: Page;
  readonly confirmationMessage: Locator;
  readonly appointmentDetails: Locator;
  readonly downloadPDFButton: Locator;
  readonly calendarSyncButton: Locator;
  readonly preferredSlotInfo: Locator;

  constructor(page: Page) {
    this.page = page;
    this.confirmationMessage = page.getByTestId('confirmation-message');
    this.appointmentDetails = page.getByTestId('appointment-details');
    this.downloadPDFButton = page.getByTestId('download-pdf');
    this.calendarSyncButton = page.getByTestId('sync-calendar');
    this.preferredSlotInfo = page.getByTestId('preferred-slot-info');
  }

  /**
   * Verify confirmation message is displayed
   */
  async verifyConfirmationMessage(expectedText: string = 'Appointment confirmed'): Promise<void> {
    await expect(this.confirmationMessage).toBeVisible();
    await expect(this.confirmationMessage).toContainText(expectedText);
  }

  /**
   * Verify appointment details
   */
  async verifyAppointmentDetails(data: {
    provider?: string;
    date?: string;
    time?: string;
  }): Promise<void> {
    if (data.provider) {
      await expect(this.appointmentDetails).toContainText(data.provider);
    }
    if (data.date) {
      await expect(this.appointmentDetails).toContainText(data.date);
    }
    if (data.time) {
      await expect(this.appointmentDetails).toContainText(data.time);
    }
  }

  /**
   * Verify PDF download button is visible
   */
  async verifyPDFDownloadAvailable(): Promise<void> {
    await expect(this.downloadPDFButton).toBeVisible();
  }

  /**
   * Download appointment confirmation PDF
   */
  async downloadPDF(): Promise<void> {
    await this.downloadPDFButton.click();
  }

  /**
   * Verify preferred slot info is displayed
   */
  async verifyPreferredSlotInfo(preferredTime: string): Promise<void> {
    await expect(this.preferredSlotInfo).toBeVisible();
    await expect(this.preferredSlotInfo).toContainText(`Preferred: ${preferredTime}`);
  }

  /**
   * Sync to calendar
   */
  async syncToCalendar(): Promise<void> {
    await this.calendarSyncButton.click();
  }
}
