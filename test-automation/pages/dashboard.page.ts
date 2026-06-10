import { type Locator, type Page, expect } from '@playwright/test';

/**
 * Page Object Model for Dashboard Pages
 * Handles role-specific dashboards
 */
export class DashboardPage {
  readonly page: Page;
  readonly patientDashboard: Locator;
  readonly staffDashboard: Locator;
  readonly adminDashboard: Locator;
  readonly logoutButton: Locator;
  readonly appointmentsLink: Locator;
  readonly profileLink: Locator;
  readonly queueLink: Locator;

  constructor(page: Page) {
    this.page = page;
    this.patientDashboard = page.getByTestId('patient-dashboard');
    this.staffDashboard = page.getByTestId('staff-dashboard');
    this.adminDashboard = page.getByTestId('admin-dashboard');
    this.logoutButton = page.getByTestId('logout-button');
    this.appointmentsLink = page.getByTestId('appointments-link');
    this.profileLink = page.getByTestId('profile-link');
    this.queueLink = page.getByTestId('queue-link');
  }

  /**
   * Verify patient dashboard is visible
   */
  async verifyPatientDashboard(): Promise<void> {
    await expect(this.patientDashboard).toBeVisible();
  }

  /**
   * Verify staff dashboard is visible
   */
  async verifyStaffDashboard(): Promise<void> {
    await expect(this.staffDashboard).toBeVisible();
  }

  /**
   * Verify admin dashboard is visible
   */
  async verifyAdminDashboard(): Promise<void> {
    await expect(this.adminDashboard).toBeVisible();
  }

  /**
   * Click logout button
   */
  async logout(): Promise<void> {
    await this.logoutButton.click();
  }

  /**
   * Navigate to appointments
   */
  async goToAppointments(): Promise<void> {
    await this.appointmentsLink.click();
  }

  /**
   * Navigate to profile
   */
  async goToProfile(): Promise<void> {
    await this.profileLink.click();
  }

  /**
   * Navigate to queue (staff only)
   */
  async goToQueue(): Promise<void> {
    await this.queueLink.click();
  }
}
