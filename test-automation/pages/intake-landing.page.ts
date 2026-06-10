import { type Locator, type Page } from '@playwright/test';

/**
 * Page Object Model for Intake Landing Page
 * Handles initial intake mode selection
 */
export class IntakeLandingPage {
  readonly page: Page;
  readonly aiModeButton: Locator;
  readonly manualModeButton: Locator;
  readonly modeSwitcher: Locator;

  constructor(page: Page) {
    this.page = page;
    this.aiModeButton = page.getByTestId('ai-mode-button');
    this.manualModeButton = page.getByTestId('manual-mode-button');
    this.modeSwitcher = page.getByTestId('mode-switcher');
  }

  /**
   * Navigate to intake page
   */
  async goto(): Promise<void> {
    await this.page.goto('/intake');
  }

  /**
   * Select AI conversational mode
   */
  async selectAIMode(): Promise<void> {
    await this.aiModeButton.click();
  }

  /**
   * Select manual form mode
   */
  async selectManualMode(): Promise<void> {
    await this.manualModeButton.click();
  }
}
