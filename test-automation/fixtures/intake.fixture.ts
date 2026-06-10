import { test as base } from '@playwright/test';

/**
 * Test data for patient intake scenarios
 */
export const intakeTestData = {
  diabeticPatient: {
    medicalHistory: 'Type 2 Diabetes (2015), Hypertension (2018)',
    medications: ['Metformin 500mg twice daily', 'Lisinopril 10mg once daily'],
    allergies: ['Penicillin'],
    symptoms: 'Occasional headaches, mild dizziness',
    conditions: ['diabetes', 'hypertension'],
  },
  healthyPatient: {
    medicalHistory: 'No chronic conditions',
    medications: [],
    allergies: ['None'],
    symptoms: 'Routine checkup',
    conditions: [],
  },
  allergyPatient: {
    medicalHistory: 'Seasonal allergies, occasional migraines',
    medications: ['Zyrtec 10mg daily'],
    allergies: ['Pollen (environmental only)'],
    symptoms: 'Sneezing, itchy eyes',
    conditions: [],
  },
};

/**
 * Test user credentials
 */
export const testUsers = {
  patient: {
    email: 'patient@example.com',
    password: 'PatientPass123!',
    name: 'Test Patient',
  },
  staff: {
    email: 'staff@clinic.com',
    password: 'StaffPass123!',
    name: 'Test Staff',
  },
  admin: {
    email: 'admin@clinic.com',
    password: 'AdminPass123!',
    name: 'Test Admin',
  },
};

/**
 * Extended test fixture with authentication helper
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
});

export { expect } from '@playwright/test';
