import { type Page } from '@playwright/test';

/**
 * Helper utilities for patient intake tests
 */

/**
 * Authenticate as a specific user role
 */
export async function authenticateAs(
  page: Page,
  role: 'patient' | 'staff' | 'admin',
  credentials: { email: string; password: string }
): Promise<void> {
  await page.goto('/login');
  await page.getByTestId('email-input').fill(credentials.email);
  await page.getByTestId('password-input').fill(credentials.password);
  await page.getByTestId('login-button').click();
  
  const dashboardSelector = role === 'patient' 
    ? '[data-testid="patient-dashboard"]'
    : role === 'staff'
    ? '[data-testid="staff-dashboard"]'
    : '[data-testid="admin-dashboard"]';
    
  await page.waitForSelector(dashboardSelector);
}

/**
 * Get authentication token from localStorage
 */
export async function getAuthToken(page: Page): Promise<string | null> {
  return await page.evaluate(() => localStorage.getItem('auth_token'));
}

/**
 * Make authenticated API request
 */
export async function makeAuthenticatedRequest(
  page: Page,
  method: 'GET' | 'POST' | 'PUT' | 'DELETE',
  url: string,
  body?: any
) {
  const token = await getAuthToken(page);
  
  return await page.request.fetch(url, {
    method,
    headers: {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json',
    },
    data: body,
  });
}

/**
 * Verify intake data via API
 */
export async function verifyIntakeDataViaAPI(
  page: Page,
  expectedData: {
    medical_history?: string;
    medications?: string;
    allergies?: string;
    symptoms?: string;
  }
): Promise<boolean> {
  const response = await makeAuthenticatedRequest(page, 'GET', '/api/intake');
  
  if (!response.ok()) {
    return false;
  }
  
  const actualData = await response.json();
  
  return Object.entries(expectedData).every(([key, value]) => {
    return actualData[key] === value;
  });
}

/**
 * Mock AI service responses for consistent testing
 */
export async function mockAIService(page: Page): Promise<void> {
  await page.route('**/api/ai/**', (route) => {
    const url = route.request().url();
    
    // Mock AI greeting
    if (url.includes('/ai/start')) {
      route.fulfill({
        status: 200,
        body: JSON.stringify({
          message: 'Hi! I\'ll help you complete your medical intake',
          next_question: 'Do you have any chronic conditions?'
        }),
      });
      return;
    }
    
    // Mock AI parsing
    if (url.includes('/ai/parse')) {
      const requestBody = route.request().postDataJSON();
      route.fulfill({
        status: 200,
        body: JSON.stringify({
          parsed_data: {
            medical_history: requestBody.message,
          },
          next_question: 'What medications are you currently taking?',
        }),
      });
      return;
    }
    
    route.continue();
  });
}

/**
 * Clear all intake data for a patient
 */
export async function clearIntakeData(page: Page): Promise<void> {
  await makeAuthenticatedRequest(page, 'DELETE', '/api/intake');
}

/**
 * Seed intake data for testing edits
 */
export async function seedIntakeData(
  page: Page,
  data: {
    medical_history: string;
    medications: string;
    allergies: string;
    symptoms: string;
  }
): Promise<void> {
  await makeAuthenticatedRequest(page, 'POST', '/api/intake', data);
}
