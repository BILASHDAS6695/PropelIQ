import { type Locator, type Page, expect } from '@playwright/test';

/**
 * Page Object Model for Walk-In Registration Page
 * Handles walk-in patient registration and queue management
 */
export class WalkInPage {
  readonly page: Page;
  readonly walkInButton: Locator;
  readonly patientSearchInput: Locator;
  readonly searchButton: Locator;
  readonly createPatientButton: Locator;
  readonly patientNameInput: Locator;
  readonly phoneInput: Locator;
  readonly emailInput: Locator;
  readonly addToQueueButton: Locator;
  readonly queueList: Locator;
  readonly markArrivedButton: Locator;
  readonly walkInTimestamp: Locator;

  constructor(page: Page) {
    this.page = page;
    this.walkInButton = page.getByTestId('walk-in-booking');
    this.patientSearchInput = page.getByTestId('patient-search');
    this.searchButton = page.getByTestId('search-button');
    this.createPatientButton = page.getByTestId('create-patient');
    this.patientNameInput = page.getByTestId('patient-name');
    this.phoneInput = page.getByTestId('phone-number');
    this.emailInput = page.getByTestId('email');
    this.addToQueueButton = page.getByTestId('add-to-queue');
    this.queueList = page.getByTestId('same-day-queue');
    this.markArrivedButton = page.getByTestId('mark-arrived');
    this.walkInTimestamp = page.getByTestId('walk-in-timestamp');
  }

  /**
   * Navigate to walk-in page
   */
  async goto(): Promise<void> {
    await this.page.goto('/staff/walk-ins');
  }

  /**
   * Click walk-in button
   */
  async clickWalkInButton(): Promise<void> {
    await this.walkInButton.click();
  }

  /**
   * Search for existing patient
   */
  async searchPatient(patientName: string): Promise<void> {
    await this.patientSearchInput.fill(patientName);
    await this.searchButton.click();
  }

  /**
   * Create new patient
   */
  async createNewPatient(data: {
    name: string;
    phone: string;
    email: string;
  }): Promise<void> {
    await this.createPatientButton.click();
    await this.patientNameInput.fill(data.name);
    await this.phoneInput.fill(data.phone);
    await this.emailInput.fill(data.email);
  }

  /**
   * Add patient to same-day queue
   */
  async addToQueue(): Promise<void> {
    await this.addToQueueButton.click();
  }

  /**
   * Register walk-in patient (complete flow)
   */
  async registerWalkIn(data: {
    name: string;
    phone: string;
    email: string;
  }): Promise<void> {
    await this.clickWalkInButton();
    await this.searchPatient(data.name);
    await this.createNewPatient(data);
    await this.addToQueue();
  }

  /**
   * Verify patient is in queue
   */
  async verifyPatientInQueue(patientName: string): Promise<void> {
    await expect(this.queueList.getByText(patientName)).toBeVisible();
  }

  /**
   * Verify timestamp is recorded
   */
  async verifyTimestamp(): Promise<void> {
    await expect(this.walkInTimestamp).toBeVisible();
  }

  /**
   * Select patient in queue
   */
  async selectPatientInQueue(patientName: string): Promise<void> {
    const sanitizedName = patientName.toLowerCase().replace(/\s+/g, '-');
    await this.page.getByTestId(`patient-${sanitizedName}`).click();
  }

  /**
   * Mark patient as arrived
   */
  async markAsArrived(): Promise<void> {
    await this.markArrivedButton.click();
  }

  /**
   * Verify patient arrival status
   */
  async verifyArrivalStatus(patientName: string, status: string = 'Arrived'): Promise<void> {
    const sanitizedName = patientName.toLowerCase().replace(/\s+/g, '-');
    await expect(this.page.getByTestId(`patient-status-${sanitizedName}`)).toContainText(status);
  }
}
