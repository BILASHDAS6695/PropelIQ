import { test as base } from '@playwright/test';

/**
 * E2E Journey test data
 */
export const e2eTestData = {
  newPatient: {
    name: 'Emma Johnson',
    email: 'emma.johnson@example.com',
    phone: '555-2468',
    password: 'SecurePass123!',
  },
  appointmentBooking: {
    provider: 'Dr. Smith',
    date: '2026-06-20',
    time: '02:00 PM',
    preferredTime: '11:00 AM',
  },
  intakeData: {
    medicalHistory: 'I have seasonal allergies and occasional migraines',
    medications: 'I take Zyrtec 10mg daily for allergies',
    allergies: 'No drug allergies, only environmental allergies to pollen',
    symptoms: 'Currently experiencing seasonal allergy symptoms - sneezing and itchy eyes',
  },
  calendarProvider: 'Google Calendar',
};

/**
 * E2E Journey fixtures
 */
export const test = base.extend({
  // Clean database before E2E test
  cleanDatabase: async ({ page }, use) => {
    // Clean up any existing test data
    await page.request.post('/api/test/cleanup', {
      data: { email: e2eTestData.newPatient.email },
    });
    
    await use(page);
    
    // Cleanup after test
    await page.request.post('/api/test/cleanup', {
      data: { email: e2eTestData.newPatient.email },
    });
  },
  
  // Seed provider availability
  seededAvailability: async ({ page }, use) => {
    await page.request.post('/api/test/seed-availability', {
      data: {
        provider: 'Dr. Smith',
        date: '2026-06-20',
        availableSlots: ['09:00 AM', '10:00 AM', '02:00 PM'],
        unavailableSlots: ['11:00 AM'],
      },
    });
    
    await use(page);
  },
});

export { expect } from '@playwright/test';
