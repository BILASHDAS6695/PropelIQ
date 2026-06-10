import { type Locator, type Page, expect } from '@playwright/test';

/**
 * Page Object Model for Staff Booking Page
 * Handles staff booking appointments for patients
 */
export class StaffBookingPage {
  readonly page: Page;
  readonly patientSearchInput: Locator;
  readonly searchPatientButton: Locator;
  readonly createPatientButton: Locator;
  readonly providerSelect: Locator;
  readonly datePicker: Locator;
  readonly slotsList: Locator;
  readonly bookButton: Locator;
  readonly successMessage: Locator;

  constructor(page: Page) {
    this.page = page;
    this.patientSearchInput = page.getByTestId('patient-search');
    this.searchPatientButton = page.getByTestId('search-patient');
    this.createPatientButton = page.getByTestId('create-patient');
    this.providerSelect = page.getByTestId('provider-select');
    this.datePicker = page.getByTestId('date-picker');
    this.slotsList = page.getByTestId('slots-list');
    this.bookButton = page.getByTestId('book-appointment');
    this.successMessage = page.getByTestId('success-message');
  }

  /**
   * Navigate to staff booking page
   */
  async goto(): Promise<void> {
    await this.page.goto('/staff/bookings');
  }

  /**
   * Search for a patient
   */
  async searchPatient(patientName: string): Promise<void> {
    await this.patientSearchInput.fill(patientName);
    await this.searchPatientButton.click();
  }

  /**
   * Select a patient from search results
   */
  async selectPatient(patientName: string): Promise<void> {
    const sanitizedName = patientName.toLowerCase().replace(/\s+/g, '-');
    await this.page.getByTestId(`patient-result-${sanitizedName}`).click();
  }

  /**
   * Select provider and date
   */
  async selectProviderAndDate(provider: string, date: string): Promise<void> {
    await this.providerSelect.selectOption(provider);
    await this.datePicker.fill(date);
  }

  /**
   * Select an available slot
   */
  async selectSlot(timeSlot: string): Promise<void> {
    const slotId = timeSlot.replace(/[: ]/g, '-');
    await this.page.getByTestId(`slot-${slotId}`).click();
  }

  /**
   * Book the appointment
   */
  async bookAppointment(): Promise<void> {
    await this.bookButton.click();
  }

  /**
   * Complete booking flow for patient
   */
  async bookForPatient(data: {
    patientName: string;
    provider: string;
    date: string;
    timeSlot: string;
  }): Promise<void> {
    await this.searchPatient(data.patientName);
    await this.selectPatient(data.patientName);
    await this.selectProviderAndDate(data.provider, data.date);
    await this.selectSlot(data.timeSlot);
    await this.bookAppointment();
  }

  /**
   * Verify booking success message
   */
  async verifyBookingSuccess(patientName: string): Promise<void> {
    await expect(this.successMessage).toBeVisible();
    await expect(this.successMessage).toContainText(`Appointment booked for ${patientName}`);
  }
}
