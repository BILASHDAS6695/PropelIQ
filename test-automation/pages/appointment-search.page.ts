import { type Locator, type Page, expect } from '@playwright/test';

/**
 * Page Object Model for Appointment Search Page
 * Handles searching for available appointment slots
 */
export class AppointmentSearchPage {
  readonly page: Page;
  readonly providerDropdown: Locator;
  readonly datePicker: Locator;
  readonly searchButton: Locator;
  readonly slotsContainer: Locator;
  readonly preferredSlotCheckbox: Locator;
  readonly preferredSlotSection: Locator;
  readonly showAllSlotsButton: Locator;
  readonly confirmBookingButton: Locator;

  constructor(page: Page) {
    this.page = page;
    this.providerDropdown = page.getByTestId('provider-select');
    this.datePicker = page.getByTestId('date-picker');
    this.searchButton = page.getByTestId('search-slots-button');
    this.slotsContainer = page.getByTestId('available-slots');
    this.preferredSlotCheckbox = page.getByTestId('preferred-slot-checkbox');
    this.preferredSlotSection = page.getByTestId('preferred-slot-section');
    this.showAllSlotsButton = page.getByTestId('show-all-slots');
    this.confirmBookingButton = page.getByTestId('confirm-booking');
  }

  /**
   * Navigate to appointment booking page
   */
  async goto(): Promise<void> {
    await this.page.goto('/appointments/book');
  }

  /**
   * Select a provider
   */
  async selectProvider(providerName: string): Promise<void> {
    await this.providerDropdown.selectOption(providerName);
  }

  /**
   * Select a date
   */
  async selectDate(date: string): Promise<void> {
    await this.datePicker.fill(date);
  }

  /**
   * Click search button
   */
  async search(): Promise<void> {
    await this.searchButton.click();
  }

  /**
   * Search for appointments (complete flow)
   */
  async searchAppointments(provider: string, date: string): Promise<void> {
    await this.selectProvider(provider);
    await this.selectDate(date);
    await this.search();
  }

  /**
   * Select a time slot
   */
  async selectSlot(timeSlot: string): Promise<void> {
    const slotId = timeSlot.replace(/[: ]/g, '-');
    await this.page.getByTestId(`slot-${slotId}`).click();
  }

  /**
   * Verify available slots are displayed
   */
  async verifySlotsDisplayed(expectedSlot: string): Promise<void> {
    await expect(this.slotsContainer).toContainText(expectedSlot);
  }

  /**
   * Expand preferred slot section
   */
  async expandPreferredSlotSection(): Promise<void> {
    await this.preferredSlotSection.click();
  }

  /**
   * Show all available slots including unavailable ones
   */
  async showAllSlots(): Promise<void> {
    await this.showAllSlotsButton.click();
  }

  /**
   * Select a preferred slot
   */
  async selectPreferredSlot(timeSlot: string): Promise<void> {
    const slotId = timeSlot.replace(/[: ]/g, '-');
    await this.page.getByTestId(`slot-${slotId}-preferred`).click();
    await this.preferredSlotCheckbox.check();
  }

  /**
   * Confirm booking
   */
  async confirmBooking(): Promise<void> {
    await this.confirmBookingButton.click();
  }

  /**
   * Complete booking flow with preferred slot
   */
  async bookWithPreferredSlot(data: {
    provider: string;
    date: string;
    selectedSlot: string;
    preferredSlot: string;
  }): Promise<void> {
    await this.searchAppointments(data.provider, data.date);
    await this.selectSlot(data.selectedSlot);
    await this.expandPreferredSlotSection();
    await this.showAllSlots();
    await this.selectPreferredSlot(data.preferredSlot);
    await this.confirmBooking();
  }
}
