import { test, expect } from '@playwright/test';
import {
  LoginPage,
  RegistrationPage,
} from '../pages';

test.describe('Authentication & Access Control - Error Scenarios', () => {
  
  test('TW-AUTH-007: Registration Rejected - Weak Password', async ({ page }) => {
    const registrationPage = new RegistrationPage(page);

    // Navigate to registration
    await registrationPage.goto();

    // Fill form with weak password
    await registrationPage.register({
      name: 'Test User',
      email: 'test@example.com',
      phone: '555-1234',
      password: 'weak',
      confirmPassword: 'weak',
    });

    // Verify validation error
    await registrationPage.verifyPasswordError('Password must be at least 10 characters');

    // Checkpoint: Verify user not created
    const response = await page.request.get('/api/users/count?email=test@example.com');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data.count).toBe(0);
  });

  test('TW-AUTH-008: Login Fails With Wrong Password', async ({ page }) => {
    const loginPage = new LoginPage(page);

    // Precondition: Existing user
    // Navigate to login
    await loginPage.goto();

    // Enter valid email, wrong password
    await loginPage.login('user@example.com', 'WrongPassword123!');

    // Verify error message (generic - doesn't reveal user existence)
    await loginPage.verifyErrorMessage('Invalid credentials');

    // Verify not redirected
    await expect(page).toHaveURL(/.*login/);

    // Verify no JWT token issued
    const authToken = await page.evaluate(() => localStorage.getItem('auth_token'));
    expect(authToken).toBeNull();

    // Verify no cookie set
    const cookies = await page.context().cookies();
    const authCookie = cookies.find(c => c.name === 'auth_token');
    expect(authCookie).toBeUndefined();
  });

  test('TW-AUTH-009: Patient Cannot Create User Accounts', async ({ page }) => {
    const loginPage = new LoginPage(page);

    // Precondition: Login as Patient
    await loginPage.goto();
    await loginPage.login('patient@example.com', 'PatientPass123!');

    // Attempt direct API call to create user
    const response = await page.request.post('/api/admin/users', {
      headers: {
        'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
        'Content-Type': 'application/json',
      },
      data: {
        name: 'Hacker User',
        email: 'hacker@evil.com',
        role: 'Admin',
      },
    });

    // Verify 403 Forbidden response
    expect(response.status()).toBe(403);

    // Checkpoint: Verify user not created
    const verifyResponse = await page.request.get('/api/users/count?email=hacker@evil.com');
    expect(verifyResponse.ok()).toBeTruthy();
    const data = await verifyResponse.json();
    expect(data.count).toBe(0);

    // Checkpoint: Verify unauthorized attempt logged
    const auditResponse = await page.request.get('/api/audit-logs?action=UNAUTHORIZED_ATTEMPT', {
      headers: {
        'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
      },
    });
    
    if (auditResponse.ok()) {
      const auditLogs = await auditResponse.json();
      expect(auditLogs).toContainEqual(
        expect.objectContaining({
          action: 'UNAUTHORIZED_ATTEMPT',
          endpoint: '/api/admin/users',
        })
      );
    }
  });
});
