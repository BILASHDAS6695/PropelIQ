import { type Page } from '@playwright/test';

/**
 * Authentication helper utilities
 */

/**
 * Login helper function
 */
export async function login(
  page: Page,
  email: string,
  password: string
): Promise<void> {
  await page.goto('/login');
  await page.getByTestId('email-input').fill(email);
  await page.getByTestId('password-input').fill(password);
  await page.getByTestId('login-button').click();
}

/**
 * Logout helper function
 */
export async function logout(page: Page): Promise<void> {
  await page.getByTestId('logout-button').click();
}

/**
 * Get authentication token from localStorage
 */
export async function getAuthToken(page: Page): Promise<string | null> {
  return await page.evaluate(() => localStorage.getItem('auth_token'));
}

/**
 * Get authentication token from cookies
 */
export async function getAuthCookie(page: Page): Promise<string | undefined> {
  const cookies = await page.context().cookies();
  const authCookie = cookies.find(c => c.name === 'auth_token');
  return authCookie?.value;
}

/**
 * Clear authentication state
 */
export async function clearAuth(page: Page): Promise<void> {
  await page.evaluate(() => {
    localStorage.removeItem('auth_token');
    sessionStorage.clear();
  });
  await page.context().clearCookies();
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
 * Verify user role via API
 */
export async function verifyUserRole(
  page: Page,
  email: string,
  expectedRole: string
): Promise<boolean> {
  const response = await makeAuthenticatedRequest(page, 'GET', `/api/users?email=${email}`);
  
  if (!response.ok()) {
    return false;
  }
  
  const userData = await response.json();
  return userData.role === expectedRole;
}

/**
 * Verify user exists in database
 */
export async function verifyUserExists(
  page: Page,
  email: string
): Promise<boolean> {
  const response = await page.request.get(`/api/users/count?email=${email}`);
  
  if (!response.ok()) {
    return false;
  }
  
  const data = await response.json();
  return data.count > 0;
}

/**
 * Create test user via API (for test setup)
 */
export async function createTestUser(
  page: Page,
  userData: {
    name: string;
    email: string;
    password: string;
    role: string;
  }
): Promise<void> {
  await page.request.post('/api/admin/users', {
    headers: {
      'Authorization': `Bearer ${await getAuthToken(page)}`,
      'Content-Type': 'application/json',
    },
    data: userData,
  });
}

/**
 * Delete test user via API (for test cleanup)
 */
export async function deleteTestUser(
  page: Page,
  email: string
): Promise<void> {
  await page.request.delete(`/api/admin/users?email=${email}`, {
    headers: {
      'Authorization': `Bearer ${await getAuthToken(page)}`,
    },
  });
}

/**
 * Verify audit log entry
 */
export async function verifyAuditLog(
  page: Page,
  action: string,
  targetEmail?: string
): Promise<boolean> {
  const response = await makeAuthenticatedRequest(
    page,
    'GET',
    `/api/admin/audit-logs?action=${action}`
  );
  
  if (!response.ok()) {
    return false;
  }
  
  const auditLogs = await response.json();
  
  if (targetEmail) {
    return auditLogs.some((log: any) => 
      log.action === action && log.target_email === targetEmail
    );
  }
  
  return auditLogs.some((log: any) => log.action === action);
}

/**
 * Mock email service for testing
 */
export async function mockEmailService(page: Page): Promise<void> {
  await page.route('**/api/email/**', (route) => {
    route.fulfill({
      status: 200,
      body: JSON.stringify({
        success: true,
        message: 'Email sent',
      }),
    });
  });
}

/**
 * Verify email was sent (check mock or database)
 */
export async function verifyEmailSent(
  page: Page,
  to: string,
  subject: string
): Promise<boolean> {
  const response = await makeAuthenticatedRequest(
    page,
    'GET',
    `/api/admin/emails?to=${to}&subject=${encodeURIComponent(subject)}`
  );
  
  if (!response.ok()) {
    return false;
  }
  
  const emails = await response.json();
  return emails.length > 0;
}

/**
 * Wait for session to expire (or use clock mocking)
 */
export async function waitForSessionExpiry(
  page: Page,
  timeoutMs: number = 900000 // 15 minutes default
): Promise<void> {
  await page.waitForTimeout(timeoutMs);
}

/**
 * Mock system clock for session timeout testing
 */
export async function mockClock(page: Page, initialTime?: Date): Promise<void> {
  await page.addInitScript((time) => {
    // Mock Date.now() to control time
    const startTime = time ? new Date(time).getTime() : Date.now();
    let currentTime = startTime;
    
    (window as any).__advanceTime = (ms: number) => {
      currentTime += ms;
    };
    
    const OriginalDate = Date;
    (window as any).Date = class extends OriginalDate {
      constructor(...args: any[]) {
        if (args.length === 0) {
          super(currentTime);
        } else {
          super(...args);
        }
      }
      
      static now() {
        return currentTime;
      }
    };
  }, initialTime?.toISOString());
}

/**
 * Advance mocked clock by specified milliseconds
 */
export async function advanceTime(page: Page, ms: number): Promise<void> {
  await page.evaluate((milliseconds) => {
    (window as any).__advanceTime(milliseconds);
  }, ms);
}
