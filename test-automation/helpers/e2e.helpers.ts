import { type Page } from '@playwright/test';

/**
 * E2E Journey helper utilities
 */

/**
 * Simulate account activation (for testing without actual email)
 */
export async function activateTestAccount(
  page: Page,
  email: string
): Promise<void> {
  const response = await page.request.post('/api/test/activate-account', {
    data: { email },
  });
  
  if (!response.ok()) {
    throw new Error(`Failed to activate account for ${email}`);
  }
}

/**
 * Retrieve activation link from email (mock for testing)
 */
export async function getActivationLink(
  page: Page,
  email: string
): Promise<string> {
  const response = await page.request.get(`/api/test/emails?to=${email}&subject=Activate`, {
    headers: {
      'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
    },
  });
  
  if (response.ok()) {
    const emails = await response.json();
    if (emails.length > 0) {
      // Extract activation URL from email body
      const activationLink = emails[0].body.match(/https?:\/\/[^\s]+activate[^\s]+/);
      return activationLink ? activationLink[0] : '';
    }
  }
  
  return '';
}

/**
 * Verify email was received
 */
export async function verifyEmailReceived(
  page: Page,
  to: string,
  subject: string,
  bodyContains?: string
): Promise<boolean> {
  const response = await page.request.get(`/api/test/emails?to=${to}&subject=${encodeURIComponent(subject)}`);
  
  if (!response.ok()) {
    return false;
  }
  
  const emails = await response.json();
  
  if (emails.length === 0) {
    return false;
  }
  
  if (bodyContains) {
    return emails.some((email: any) => email.body.includes(bodyContains));
  }
  
  return true;
}

/**
 * Verify SMS was sent
 */
export async function verifySMSSent(
  page: Page,
  to: string,
  messageContains: string
): Promise<boolean> {
  const response = await page.request.get(`/api/test/sms?to=${to}`);
  
  if (!response.ok()) {
    return false;
  }
  
  const messages = await response.json();
  return messages.some((msg: any) => msg.body.includes(messageContains));
}

/**
 * Verify PDF was generated and downloaded
 */
export async function verifyPDFDownload(
  page: Page,
  expectedFilename: string
): Promise<boolean> {
  try {
    const downloadPromise = page.waitForEvent('download', { timeout: 5000 });
    const download = await downloadPromise;
    return download.suggestedFilename().includes(expectedFilename);
  } catch {
    return false;
  }
}

/**
 * Mock OAuth flow for calendar integration
 */
export async function mockCalendarOAuth(page: Page): Promise<void> {
  await page.route('**/api/calendar/google/authorize', (route) => {
    route.fulfill({
      status: 200,
      body: JSON.stringify({
        success: true,
        message: 'Calendar synced',
        token: 'mock_google_token',
      }),
    });
  });
  
  await page.route('**/api/calendar/google/callback', (route) => {
    route.fulfill({
      status: 200,
      body: JSON.stringify({
        success: true,
      }),
    });
  });
}

/**
 * Verify calendar event was created
 */
export async function verifyCalendarEventCreated(
  page: Page,
  appointmentId: string
): Promise<boolean> {
  const response = await page.request.get(`/api/appointments/${appointmentId}/calendar-sync`, {
    headers: {
      'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
    },
  });
  
  if (!response.ok()) {
    return false;
  }
  
  const data = await response.json();
  return data.synced === true;
}

/**
 * Verify appointment reminders are scheduled
 */
export async function verifyRemindersScheduled(
  page: Page,
  appointmentId: string
): Promise<{
  email24h: boolean;
  sms24h: boolean;
  email2h: boolean;
  sms2h: boolean;
}> {
  const response = await page.request.get(`/api/appointments/${appointmentId}/reminders`, {
    headers: {
      'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
    },
  });
  
  if (!response.ok()) {
    return {
      email24h: false,
      sms24h: false,
      email2h: false,
      sms2h: false,
    };
  }
  
  const reminders = await response.json();
  
  return {
    email24h: reminders.some((r: any) => r.type === 'email' && r.hours_before === 24),
    sms24h: reminders.some((r: any) => r.type === 'sms' && r.hours_before === 24),
    email2h: reminders.some((r: any) => r.type === 'email' && r.hours_before === 2),
    sms2h: reminders.some((r: any) => r.type === 'sms' && r.hours_before === 2),
  };
}

/**
 * Get complete patient journey data
 */
export async function getPatientJourneyData(
  page: Page,
  patientEmail: string
): Promise<{
  user: any;
  appointments: any[];
  intake: any;
  auditLogs: any[];
}> {
  // Get user data
  const userResponse = await page.request.get(`/api/users?email=${patientEmail}`, {
    headers: {
      'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
    },
  });
  const user = userResponse.ok() ? await userResponse.json() : null;
  
  // Get appointments
  const appointmentsResponse = await page.request.get('/api/appointments', {
    headers: {
      'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
    },
  });
  const appointments = appointmentsResponse.ok() ? await appointmentsResponse.json() : [];
  
  // Get intake data
  const intakeResponse = await page.request.get('/api/intake', {
    headers: {
      'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
    },
  });
  const intake = intakeResponse.ok() ? await intakeResponse.json() : null;
  
  // Get audit logs
  const auditResponse = await page.request.get('/api/admin/audit-logs', {
    headers: {
      'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
    },
  });
  const auditLogs = auditResponse.ok() ? await auditResponse.json() : [];
  
  return {
    user,
    appointments,
    intake,
    auditLogs,
  };
}

/**
 * Clean up test data after E2E journey
 */
export async function cleanupTestPatient(
  page: Page,
  patientEmail: string
): Promise<void> {
  await page.request.delete(`/api/test/users?email=${patientEmail}`, {
    headers: {
      'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('auth_token'))}`,
    },
  });
}

/**
 * Seed provider availability for E2E test
 */
export async function seedE2EProviderAvailability(page: Page): Promise<void> {
  await page.request.post('/api/test/seed-availability', {
    data: {
      provider: 'Dr. Smith',
      date: '2026-06-20',
      availableSlots: ['09:00 AM', '10:00 AM', '02:00 PM'],
      unavailableSlots: ['11:00 AM'],
    },
  });
}

/**
 * Verify complete E2E journey success criteria
 */
export async function verifyJourneyCompletionCriteria(
  page: Page,
  patientEmail: string,
  appointmentId: string
): Promise<{
  userAccountExists: boolean;
  appointmentBooked: boolean;
  intakeCompleted: boolean;
  calendarSynced: boolean;
  remindersScheduled: boolean;
  arrivalLogged: boolean;
}> {
  const journeyData = await getPatientJourneyData(page, patientEmail);
  
  const appointment = journeyData.appointments.find((a: any) => a.id === appointmentId);
  
  return {
    userAccountExists: journeyData.user !== null && journeyData.user.status === 'active',
    appointmentBooked: appointment !== undefined && appointment.status !== 'cancelled',
    intakeCompleted: journeyData.intake !== null && journeyData.intake.completed === true,
    calendarSynced: appointment?.calendar_synced === true,
    remindersScheduled: appointment?.reminders_scheduled === true,
    arrivalLogged: appointment?.status === 'arrived' && appointment?.arrival_timestamp !== null,
  };
}

/**
 * Measure E2E test execution time
 */
export function createJourneyTimer() {
  const startTime = Date.now();
  
  return {
    getElapsedTime: () => {
      return Date.now() - startTime;
    },
    getElapsedMinutes: () => {
      return Math.floor((Date.now() - startTime) / 60000);
    },
    getElapsedSeconds: () => {
      return Math.floor((Date.now() - startTime) / 1000);
    },
  };
}

/**
 * Wait for background job completion (reminders, etc.)
 */
export async function waitForBackgroundJob(
  page: Page,
  jobType: string,
  maxWaitMs: number = 10000
): Promise<boolean> {
  const startTime = Date.now();
  
  while (Date.now() - startTime < maxWaitMs) {
    const response = await page.request.get(`/api/test/jobs?type=${jobType}`);
    
    if (response.ok()) {
      const jobs = await response.json();
      const completedJob = jobs.find((job: any) => job.status === 'completed');
      
      if (completedJob) {
        return true;
      }
    }
    
    await page.waitForTimeout(1000);
  }
  
  return false;
}
