import { type Locator, type Page, expect } from '@playwright/test';

/**
 * Page Object Model for Registration Page
 * Handles patient self-registration
 */
export class RegistrationPage {
  readonly page: Page;
  readonly nameInput: Locator;
  readonly emailInput: Locator;
  readonly phoneInput: Locator;
  readonly passwordInput: Locator;
  readonly confirmPasswordInput: Locator;
  readonly submitButton: Locator;
  readonly successMessage: Locator;
  readonly errorMessage: Locator;
  readonly passwordError: Locator;

  constructor(page: Page) {
    this.page = page;
    this.nameInput = page.getByTestId('name-input');
    this.emailInput = page.getByTestId('email-input');
    this.phoneInput = page.getByTestId('phone-input');
    this.passwordInput = page.getByTestId('password-input');
    this.confirmPasswordInput = page.getByTestId('confirm-password-input');
    this.submitButton = page.getByTestId('register-button');
    this.successMessage = page.getByTestId('success-message');
    this.errorMessage = page.getByTestId('error-message');
    this.passwordError = page.getByTestId('password-error');
  }

  /**
   * Navigate to registration page
   */
  async goto(): Promise<void> {
    await this.page.goto('/register');
  }

  /**
   * Fill registration form
   */
  async fillRegistrationForm(data: {
    name: string;
    email: string;
    phone: string;
    password: string;
    confirmPassword: string;
  }): Promise<void> {
    await this.nameInput.fill(data.name);
    await this.emailInput.fill(data.email);
    await this.phoneInput.fill(data.phone);
    await this.passwordInput.fill(data.password);
    await this.confirmPasswordInput.fill(data.confirmPassword);
  }

  /**
   * Submit registration form
   */
  async submit(): Promise<void> {
    await this.submitButton.click();
  }

  /**
   * Complete registration flow
   */
  async register(data: {
    name: string;
    email: string;
    phone: string;
    password: string;
    confirmPassword: string;
  }): Promise<void> {
    await this.fillRegistrationForm(data);
    await this.submit();
  }

  /**
   * Verify success message
   */
  async verifySuccess(): Promise<void> {
    await expect(this.successMessage).toBeVisible();
  }

  /**
   * Verify error message
   */
  async verifyErrorMessage(expectedText: string): Promise<void> {
    await expect(this.errorMessage).toBeVisible();
    await expect(this.errorMessage).toContainText(expectedText);
  }

  /**
   * Verify password validation error
   */
  async verifyPasswordError(expectedText: string): Promise<void> {
    await expect(this.passwordError).toBeVisible();
    await expect(this.passwordError).toContainText(expectedText);
  }
}
