import { test as base } from '@playwright/test';

/**
 * Test user credentials for authentication tests
 */
export const testUsers = {
  patient: {
    email: 'patient@example.com',
    password: 'PatientPass123!',
    name: 'Test Patient',
    role: 'Patient',
  },
  staff: {
    email: 'staff@clinic.com',
    password: 'StaffPass123!',
    name: 'Test Staff',
    role: 'Staff',
  },
  admin: {
    email: 'admin@clinic.com',
    password: 'AdminPass123!',
    name: 'Test Admin',
    role: 'Admin',
  },
  existingUser: {
    email: 'existing@example.com',
    password: 'ExistingPass123!',
    name: 'Existing User',
    role: 'Patient',
  },
};

/**
 * Test data for registration scenarios
 */
export const registrationTestData = {
  validPatient: {
    name: 'John Doe',
    email: 'john.doe@example.com',
    phone: '555-0123',
    password: 'SecurePass123!',
    confirmPassword: 'SecurePass123!',
  },
  weakPassword: {
    name: 'Test User',
    email: 'test@example.com',
    phone: '555-1234',
    password: 'weak',
    confirmPassword: 'weak',
  },
  duplicateEmail: {
    name: 'Another User',
    email: 'existing@example.com',
    phone: '555-9999',
    password: 'AnotherPass123!',
    confirmPassword: 'AnotherPass123!',
  },
};

/**
 * Invalid test data for error scenarios
 */
export const invalidData = {
  passwords: {
    tooShort: 'weak',
    noLetters: '12345678',
    tooCommon: 'password',
    whitespace: '   ',
    empty: '',
  },
  emails: {
    invalid: 'notanemail',
    missingLocal: '@example.com',
    missingDomain: 'user@',
  },
};

/**
 * Extended test fixture with authenticated user contexts
 */
export const test = base.extend({
  authenticatedPatientPage: async ({ page }, use) => {
    // Login as patient
    await page.goto('/login');
    await page.getByTestId('email-input').fill(testUsers.patient.email);
    await page.getByTestId('password-input').fill(testUsers.patient.password);
    await page.getByTestId('login-button').click();
    
    // Wait for dashboard
    await page.waitForSelector('[data-testid="patient-dashboard"]');
    
    await use(page);
  },

  authenticatedStaffPage: async ({ page }, use) => {
    // Login as staff
    await page.goto('/login');
    await page.getByTestId('email-input').fill(testUsers.staff.email);
    await page.getByTestId('password-input').fill(testUsers.staff.password);
    await page.getByTestId('login-button').click();
    
    // Wait for dashboard
    await page.waitForSelector('[data-testid="staff-dashboard"]');
    
    await use(page);
  },

  authenticatedAdminPage: async ({ page }, use) => {
    // Login as admin
    await page.goto('/login');
    await page.getByTestId('email-input').fill(testUsers.admin.email);
    await page.getByTestId('password-input').fill(testUsers.admin.password);
    await page.getByTestId('login-button').click();
    
    // Wait for dashboard
    await page.waitForSelector('[data-testid="admin-dashboard"]');
    
    await use(page);
  },
});

export { expect } from '@playwright/test';
