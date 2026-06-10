import { test, expect } from '@playwright/test';
import {
  LoginPage,
  RegistrationPage,
  AdminUserManagementPage,
  DashboardPage,
} from '../pages';

test.describe('Authentication & Access Control - Edge Cases', () => {
  
  test('TW-AUTH-005: Registration Fails With Duplicate Email', async ({ page }) => {
    const registrationPage = new RegistrationPage(page);

    // Precondition: Existing user with email
    // Assume existing@example.com already exists in database

    // Navigate to registration
    await registrationPage.goto();

    // Fill form with existing email
    await registrationPage.register({
      name: 'Another User',
      email: 'existing@example.com',
      phone: '555-9999',
      password: 'AnotherPass123!',
      confirmPassword: 'AnotherPass123!',
    });

    // Verify error message
    await registrationPage.verifyErrorMessage('Email already registered');

    // Checkpoint: Verify user not created (only one with this email exists)
    const response = await page.request.get('/api/users/count?email=existing@example.com');
    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data.count).toBe(1);
  });

  test('TW-AUTH-006: Deactivated User Access Revoked Immediately', async ({ page, context }) => {
    const loginPage = new LoginPage(page);
    const dashboardPage = new DashboardPage(page);
    const errorMessage = page.getByTestId('error-message');

    // Precondition: Existing staff user
    // Staff user logs in
    await loginPage.goto();
    await loginPage.login('staff@clinic.com', 'StaffPass123!');

    // Verify staff dashboard accessible
    await dashboardPage.verifyStaffDashboard();

    // In separate session, admin deactivates user
    const adminPage = await context.newPage();
    const adminLogin = new LoginPage(adminPage);
    const adminUserMgmt = new AdminUserManagementPage(adminPage);

    await adminLogin.goto();
    await adminLogin.login('admin@clinic.com', 'AdminPass123!');
    
    await adminUserMgmt.goto();
    await adminUserMgmt.searchUser('staff@clinic.com');
    await adminUserMgmt.deactivateUser();

    // Staff attempts to navigate (original session)
    await dashboardPage.goToQueue();

    // Verify access denied
    await expect(page).toHaveURL(/.*login/);

    // Verify error message
    await expect(errorMessage).toBeVisible();
    await expect(errorMessage).toContainText('Account deactivated');

    // Checkpoint: Verify audit log entry
    const auditResponse = await adminPage.request.get('/api/admin/audit-logs?action=DEACTIVATE_USER', {
      headers: {
        'Authorization': `Bearer ${await adminPage.evaluate(() => localStorage.getItem('auth_token'))}`,
      },
    });
    expect(auditResponse.ok()).toBeTruthy();
    const auditLogs = await auditResponse.json();
    expect(auditLogs[0]).toMatchObject({
      action: 'DEACTIVATE_USER',
      target_email: 'staff@clinic.com',
      performer_role: 'Admin',
    });

    // Cleanup
    await adminPage.close();
  });
});
