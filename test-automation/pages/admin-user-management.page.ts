import { type Locator, type Page, expect } from '@playwright/test';

/**
 * Page Object Model for Admin User Management Page
 * Handles user creation and management by administrators
 */
export class AdminUserManagementPage {
  readonly page: Page;
  readonly createUserButton: Locator;
  readonly userSearchInput: Locator;
  readonly userTable: Locator;
  readonly roleDropdown: Locator;
  readonly deactivateButton: Locator;
  readonly nameInput: Locator;
  readonly emailInput: Locator;
  readonly roleSelect: Locator;
  readonly submitUserButton: Locator;
  readonly confirmDeactivate: Locator;
  readonly deactivateUserButton: Locator;

  constructor(page: Page) {
    this.page = page;
    this.createUserButton = page.getByTestId('create-user-button');
    this.userSearchInput = page.getByTestId('user-search');
    this.userTable = page.getByTestId('user-table');
    this.roleDropdown = page.getByTestId('role-select');
    this.deactivateButton = page.getByTestId('deactivate-user');
    this.nameInput = page.getByTestId('name-input');
    this.emailInput = page.getByTestId('email-input');
    this.roleSelect = page.getByTestId('role-select');
    this.submitUserButton = page.getByTestId('submit-user-button');
    this.confirmDeactivate = page.getByTestId('confirm-deactivate');
    this.deactivateUserButton = page.getByTestId('deactivate-user-button');
  }

  /**
   * Navigate to user management page
   */
  async goto(): Promise<void> {
    await this.page.goto('/admin/users');
  }

  /**
   * Click create user button
   */
  async clickCreateUser(): Promise<void> {
    await this.createUserButton.click();
  }

  /**
   * Fill new user details
   */
  async fillUserDetails(data: {
    name: string;
    email: string;
    role: string;
  }): Promise<void> {
    await this.nameInput.fill(data.name);
    await this.emailInput.fill(data.email);
    await this.roleSelect.selectOption(data.role);
  }

  /**
   * Submit user creation
   */
  async submitUser(): Promise<void> {
    await this.submitUserButton.click();
  }

  /**
   * Create a new user (complete flow)
   */
  async createUser(data: {
    name: string;
    email: string;
    role: string;
  }): Promise<void> {
    await this.clickCreateUser();
    await this.fillUserDetails(data);
    await this.submitUser();
  }

  /**
   * Search for a user
   */
  async searchUser(searchTerm: string): Promise<void> {
    await this.userSearchInput.fill(searchTerm);
  }

  /**
   * Verify user appears in table
   */
  async verifyUserInTable(userName: string): Promise<void> {
    await expect(this.userTable.getByText(userName)).toBeVisible();
  }

  /**
   * Verify user role in table
   */
  async verifyUserRole(role: string): Promise<void> {
    await expect(this.userTable.getByText(role)).toBeVisible();
  }

  /**
   * Deactivate a user
   */
  async deactivateUser(): Promise<void> {
    await this.deactivateUserButton.click();
    await this.confirmDeactivate.click();
  }
}
