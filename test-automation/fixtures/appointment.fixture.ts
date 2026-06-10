import { test as base } from '@playwright/test';

/**
 * Provider test data
 */
export const providers = {
  drSmith: {
    name: 'Dr. Smith',
    specialty: 'General Practice',
    availableDates: ['2026-06-15', '2026-06-16', '2026-06-17'],
    slotsPerDay: [
      '09:00 AM',
      '10:00 AM',
      '11:00 AM',
      '02:00 PM',
      '03:00 PM',
      '04:00 PM',
    ],
  },
  drJones: {
    name: 'Dr. Jones',
    specialty: 'Cardiology',
    availableDates: ['2026-06-15', '2026-06-16'],
    slotsPerDay: [
      '10:00 AM',
      '11:00 AM',
      '01:00 PM',
      '03:00 PM',
    ],
  },
};

/**
 * Appointment test data
 */
export const appointmentTestData = {
  standardBooking: {
    provider: 'Dr. Smith',
    date: '2026-06-15',
    time: '10:00 AM',
  },
  withPreferredSlot: {
    provider: 'Dr. Smith',
    date: '2026-06-15',
    selectedTime: '02:00 PM',
    preferredTime: '10:00 AM',
  },
  staffBooking: {
    patientName: 'John Doe',
    patientEmail: 'john.doe@example.com',
    provider: 'Dr. Smith',
    date: '2026-06-16',
    time: '11:00 AM',
  },
  walkIn: {
    name: 'Jane Smith',
    phone: '555-9876',
    email: 'jane.smith@example.com',
  },
};

/**
 * Test patients with appointment preferences
 */
export const testPatientsWithPreferences = {
  patientA: {
    email: 'patient-a@example.com',
    password: 'PatientPass123!',
    currentAppointment: {
      time: '02:00 PM',
      date: '2026-06-15',
    },
    preferredSlot: '10:00 AM',
    registeredAt: '2026-06-10 10:00:00',
  },
  patientB: {
    email: 'patient-b@example.com',
    password: 'PatientPass123!',
    currentAppointment: {
      time: '03:00 PM',
      date: '2026-06-15',
    },
    preferredSlot: '09:00 AM',
    registeredAt: '2026-06-10 12:30:00', // Later than Patient A
  },
  patientC: {
    email: 'patient-c@example.com',
    password: 'PatientPass123!',
    currentAppointment: {
      time: '09:00 AM',
      date: '2026-06-15',
    },
  },
};

/**
 * Extended test fixture with authenticated contexts
 */
export const test = base.extend({
  authenticatedPatientPage: async ({ page }, use) => {
    await page.goto('/login');
    await page.getByTestId('email-input').fill('patient@example.com');
    await page.getByTestId('password-input').fill('PatientPass123!');
    await page.getByTestId('login-button').click();
    await page.waitForSelector('[data-testid="patient-dashboard"]');
    await use(page);
  },

  authenticatedStaffPage: async ({ page }, use) => {
    await page.goto('/login');
    await page.getByTestId('email-input').fill('staff@clinic.com');
    await page.getByTestId('password-input').fill('StaffPass123!');
    await page.getByTestId('login-button').click();
    await page.waitForSelector('[data-testid="staff-dashboard"]');
    await use(page);
  },
});

export { expect } from '@playwright/test';
