import { test, expect } from '@playwright/test';
import {
  LoginPage,
  RegistrationPage,
  AdminUserManagementPage,
  DashboardPage,
} from '../pages';

test.describe('Authentication & Access Control - Happy Path', () => {
  
  test('TW-AUTH-001: Patient Self-Registration Success', async ({ page }) => {
    const registrationPage = new RegistrationPage(page);
    const loginPage = new LoginPage(page);
    const dashboardPage = new DashboardPage(page);

    // Navigate to registration page
    await registrationPage.goto();

    // Fill registration form
    await registrationPage.register({
      name: 'John Doe',
      email: 'john.doe@example.com',
      phone: '555-0123',
      password: 'SecurePass123!',
      confirmPassword: 'SecurePass123!',
    });

    // Verify success message
    await registrationPage.verifySuccess();

    // Verify activation email sent (mock or check database)
    // TODO: Implement email verification check

    // Login with new credentials
    await loginPage.goto();
    await loginPage.login('john.doe@example.com', 'SecurePass123!');

    // Verify patient dashboard accessible
    await dashboardPage.verifyPatientDashboard();

    // Checkpoint: Verify account created via API
    const response = await page.request.get('/api/users?email=john.doe@example.com', {
      headers: {
        'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
      },
    });
    expect(response.ok()).toBeTruthy();
    const userData = await response.json();
    expect(userData.email).toBe('john.doe@example.com');
    expect(userData.name).toBe('John Doe');
  });

  test('TW-AUTH-002: Admin Creates Staff Account', async ({ page }) => {
    const loginPage = new LoginPage(page);
    const adminUserMgmt = new AdminUserManagementPage(page);

    // Precondition: Login as Admin
    await loginPage.goto();
    await loginPage.login('admin@clinic.com', 'AdminPass123!');

    // Navigate to user management
    await adminUserMgmt.goto();

    // Create staff user
    await adminUserMgmt.createUser({
      name: 'Jane Smith',
      email: 'jane.smith@clinic.com',
      role: 'Staff',
    });

    // Verify user appears in table
    await adminUserMgmt.verifyUserInTable('Jane Smith');

    // Verify role is Staff
    await adminUserMgmt.verifyUserRole('Staff');

    // Checkpoint: Verify staff account created via API
    const response = await page.request.get('/api/admin/users?email=jane.smith@clinic.com', {
      headers: {
        'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
      },
    });
    expect(response.ok()).toBeTruthy();
    const userData = await response.json();
    expect(userData.role).toBe('Staff');

    // Checkpoint: Verify audit log entry
    const auditResponse = await page.request.get('/api/admin/audit-logs?action=CREATE_USER', {
      headers: {
        'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
      },
    });
    expect(auditResponse.ok()).toBeTruthy();
    const auditLogs = await auditResponse.json();
    expect(auditLogs).toContainEqual(
      expect.objectContaining({
        action: 'CREATE_USER',
        target_email: 'jane.smith@clinic.com',
      })
    );
  });

  test('TW-AUTH-003: RBAC - Patient Cannot Access Admin Endpoints', async ({ page }) => {
    const loginPage = new LoginPage(page);
    const errorMessage = page.getByTestId('error-message');

    // Precondition: Login as Patient
    await loginPage.goto();
    await loginPage.login('patient@example.com', 'PatientPass123!');

    // Attempt to access admin dashboard
    await page.goto('/admin/dashboard');

    // Verify access denied
    await expect(errorMessage).toBeVisible();
    await expect(errorMessage).toContainText('403 Forbidden');

    // Attempt to access staff endpoints
    await page.goto('/staff/queue');

    // Verify access denied
    await expect(errorMessage).toBeVisible();
    await expect(errorMessage).toContainText('403 Forbidden');

    // Checkpoint: Verify HTTP 403 via API
    const adminResponse = await page.request.get('/api/admin/dashboard', {
      headers: {
        'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
      },
    });
    expect(adminResponse.status()).toBe(403);
  });

  test('TW-AUTH-004: Session Expires After 15 Minutes Inactivity', async ({ page }) => {
    const loginPage = new LoginPage(page);
    const dashboardPage = new DashboardPage(page);
    const infoMessage = page.getByTestId('info-message');
    const profilePage = page.getByTestId('profile-page');

    // Precondition: Login as Patient
    await loginPage.goto();
    await loginPage.login('patient@example.com', 'PatientPass123!');

    // Verify initial authenticated state
    await dashboardPage.verifyPatientDashboard();

    // Perform action at T+0
    await dashboardPage.goToAppointments();

    // Wait 14 minutes (within timeout window)
    await page.waitForTimeout(840000); // 14 minutes

    // Perform action at T+14min
    await dashboardPage.goToProfile();

    // Verify action succeeds
    await expect(profilePage).toBeVisible();

    // Wait additional 2 minutes (total 16 min - exceeds timeout)
    await page.waitForTimeout(120000); // 2 minutes

    // Attempt action after timeout
    await dashboardPage.goToAppointments();

    // Verify redirected to login
    await expect(page).toHaveURL(/.*login/);

    // Verify session expired message
    await expect(infoMessage).toContainText('Session expired');
  });
});
