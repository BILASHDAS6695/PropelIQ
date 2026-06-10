import { type Locator, type Page, expect } from '@playwright/test';

/**
 * Page Object Model for AI Conversational Intake
 * Handles AI-driven patient intake flow
 */
export class AIConversationalIntakePage {
  readonly page: Page;
  readonly chatInterface: Locator;
  readonly messageInput: Locator;
  readonly sendButton: Locator;
  readonly aiResponse: Locator;
  readonly dataSummary: Locator;
  readonly switchToManualButton: Locator;
  readonly confirmButton: Locator;
  readonly editButton: Locator;
  readonly dataSummaryReview: Locator;
  readonly summaryConditions: Locator;
  readonly summaryMedications: Locator;
  readonly summaryAllergies: Locator;
  readonly summarySymptoms: Locator;
  readonly successMessage: Locator;

  constructor(page: Page) {
    this.page = page;
    this.chatInterface = page.getByTestId('ai-chat-interface');
    this.messageInput = page.getByTestId('message-input');
    this.sendButton = page.getByTestId('send-message');
    this.aiResponse = page.getByTestId('ai-response');
    this.dataSummary = page.getByTestId('captured-data-summary');
    this.switchToManualButton = page.getByTestId('switch-to-manual');
    this.confirmButton = page.getByTestId('confirm-intake');
    this.editButton = page.getByTestId('edit-data');
    this.dataSummaryReview = page.getByTestId('data-summary-review');
    this.summaryConditions = page.getByTestId('summary-conditions');
    this.summaryMedications = page.getByTestId('summary-medications');
    this.summaryAllergies = page.getByTestId('summary-allergies');
    this.summarySymptoms = page.getByTestId('summary-symptoms');
    this.successMessage = page.getByTestId('success-message');
  }

  /**
   * Wait for AI greeting message
   */
  async waitForGreeting(): Promise<void> {
    await expect(this.aiResponse).toContainText('Hi! I\'ll help you complete your medical intake');
  }

  /**
   * Send a message to the AI
   */
  async sendMessage(message: string): Promise<void> {
    await this.messageInput.fill(message);
    await this.sendButton.click();
  }

  /**
   * Wait for AI to ask about a specific topic
   */
  async waitForQuestion(topic: string): Promise<void> {
    await this.page.waitForSelector(`[data-testid="ai-response"]:has-text("${topic}")`);
  }

  /**
   * Verify data is captured in summary
   */
  async verifyCapturedData(text: string): Promise<void> {
    await expect(this.dataSummary).toContainText(text);
  }

  /**
   * Switch to manual form mode
   */
  async switchToManual(): Promise<void> {
    await this.switchToManualButton.click();
  }

  /**
   * Verify summary data before confirmation
   */
  async verifySummaryData(data: {
    conditions?: string;
    medications?: string;
    allergies?: string;
    symptoms?: string;
  }): Promise<void> {
    if (data.conditions) {
      await expect(this.summaryConditions).toContainText(data.conditions);
    }
    if (data.medications) {
      await expect(this.summaryMedications).toContainText(data.medications);
    }
    if (data.allergies) {
      await expect(this.summaryAllergies).toContainText(data.allergies);
    }
    if (data.symptoms) {
      await expect(this.summarySymptoms).toContainText(data.symptoms);
    }
  }

  /**
   * Confirm intake completion
   */
  async confirmIntake(): Promise<void> {
    await this.confirmButton.click();
  }

  /**
   * Verify success message
   */
  async verifySuccess(): Promise<void> {
    await expect(this.successMessage).toContainText('Intake completed successfully');
  }
}
