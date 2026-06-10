import { type Page } from '@playwright/test';

/**
 * Appointment helper utilities
 */

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
 * Seed provider availability
 */
export async function seedProviderAvailability(
  page: Page,
  provider: string,
  date: string,
  slots: string[]
): Promise<void> {
  await makeAuthenticatedRequest(page, 'POST', '/api/admin/provider-availability', {
    provider,
    date,
    slots,
  });
}

/**
 * Clear all appointments for a date
 */
export async function clearAppointmentsForDate(
  page: Page,
  date: string
): Promise<void> {
  await makeAuthenticatedRequest(page, 'DELETE', `/api/admin/appointments?date=${date}`);
}

/**
 * Create appointment via API
 */
export async function createAppointment(
  page: Page,
  appointmentData: {
    patientEmail: string;
    provider: string;
    date: string;
    time: string;
    preferredSlot?: string;
  }
): Promise<any> {
  const response = await makeAuthenticatedRequest(
    page,
    'POST',
    '/api/appointments',
    appointmentData
  );
  
  if (response.ok()) {
    return await response.json();
  }
  
  throw new Error('Failed to create appointment');
}

/**
 * Cancel appointment via API
 */
export async function cancelAppointment(
  page: Page,
  appointmentId: string
): Promise<void> {
  await makeAuthenticatedRequest(
    page,
    'DELETE',
    `/api/appointments/${appointmentId}`
  );
}

/**
 * Verify slot availability
 */
export async function verifySlotAvailability(
  page: Page,
  provider: string,
  date: string,
  time: string
): Promise<boolean> {
  const response = await makeAuthenticatedRequest(
    page,
    'GET',
    `/api/slots?provider=${provider}&date=${date}&time=${encodeURIComponent(time)}`
  );
  
  if (response.ok()) {
    const data = await response.json();
    return data.available === true;
  }
  
  return false;
}

/**
 * Get appointments for a patient
 */
export async function getPatientAppointments(
  page: Page,
  patientEmail?: string
): Promise<any[]> {
  const url = patientEmail
    ? `/api/appointments?patient_email=${patientEmail}`
    : '/api/appointments';
    
  const response = await makeAuthenticatedRequest(page, 'GET', url);
  
  if (response.ok()) {
    return await response.json();
  }
  
  return [];
}

/**
 * Verify appointment exists
 */
export async function verifyAppointmentExists(
  page: Page,
  criteria: {
    provider?: string;
    date?: string;
    time?: string;
    patientEmail?: string;
  }
): Promise<boolean> {
  const appointments = await getPatientAppointments(page, criteria.patientEmail);
  
  return appointments.some((apt: any) => {
    if (criteria.provider && apt.provider !== criteria.provider) return false;
    if (criteria.date && apt.date !== criteria.date) return false;
    if (criteria.time && apt.time !== criteria.time) return false;
    return true;
  });
}

/**
 * Mock email service for appointment confirmations
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
 * Mock SMS service for appointment notifications
 */
export async function mockSMSService(page: Page): Promise<void> {
  await page.route('**/api/sms/**', (route) => {
    route.fulfill({
      status: 200,
      body: JSON.stringify({
        success: true,
        message: 'SMS sent',
      }),
    });
  });
}

/**
 * Verify email was sent
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
  
  if (response.ok()) {
    const emails = await response.json();
    return emails.length > 0;
  }
  
  return false;
}

/**
 * Verify SMS was sent
 */
export async function verifySMSSent(
  page: Page,
  to: string,
  messageContains: string
): Promise<boolean> {
  const response = await makeAuthenticatedRequest(
    page,
    'GET',
    `/api/admin/sms?to=${to}`
  );
  
  if (response.ok()) {
    const messages = await response.json();
    return messages.some((msg: any) => msg.body.includes(messageContains));
  }
  
  return false;
}

/**
 * Wait for slot swap processing
 */
export async function waitForSlotSwap(page: Page, timeoutMs: number = 5000): Promise<void> {
  await page.waitForTimeout(timeoutMs);
}

/**
 * Register preferred slot for appointment
 */
export async function registerPreferredSlot(
  page: Page,
  appointmentId: string,
  preferredTime: string
): Promise<void> {
  await makeAuthenticatedRequest(
    page,
    'PUT',
    `/api/appointments/${appointmentId}/preferred-slot`,
    { preferredTime }
  );
}

/**
 * Get same-day queue
 */
export async function getSameDayQueue(page: Page): Promise<any[]> {
  const response = await makeAuthenticatedRequest(
    page,
    'GET',
    '/api/appointments/same-day-queue'
  );
  
  if (response.ok()) {
    return await response.json();
  }
  
  return [];
}

/**
 * Mark patient as arrived
 */
export async function markPatientArrived(
  page: Page,
  appointmentId: string
): Promise<void> {
  await makeAuthenticatedRequest(
    page,
    'POST',
    `/api/appointments/${appointmentId}/arrive`
  );
}

/**
 * Verify PDF was generated
 */
export async function verifyPDFGenerated(
  page: Page,
  appointmentId: string
): Promise<boolean> {
  const response = await makeAuthenticatedRequest(
    page,
    'GET',
    `/api/appointments/${appointmentId}/pdf`
  );
  
  return response.status() === 200;
}

/**
 * Create walk-in appointment
 */
export async function createWalkInAppointment(
  page: Page,
  patientData: {
    name: string;
    phone: string;
    email: string;
  }
): Promise<any> {
  const response = await makeAuthenticatedRequest(
    page,
    'POST',
    '/api/appointments/walk-in',
    patientData
  );
  
  if (response.ok()) {
    return await response.json();
  }
  
  throw new Error('Failed to create walk-in appointment');
}

/**
 * Verify concurrent booking protection
 */
export async function verifyConcurrentBookingProtection(
  page: Page,
  provider: string,
  date: string,
  time: string
): Promise<boolean> {
  const response = await makeAuthenticatedRequest(
    page,
    'GET',
    `/api/appointments?provider=${provider}&date=${date}&time=${encodeURIComponent(time)}`
  );
  
  if (response.ok()) {
    const appointments = await response.json();
    // Should have exactly one appointment for this slot
    return appointments.count === 1;
  }
  
  return false;
}

/**
 * Get preferred slot registrations
 */
export async function getPreferredSlotRegistrations(
  page: Page,
  preferredTime: string
): Promise<any[]> {
  const response = await makeAuthenticatedRequest(
    page,
    'GET',
    `/api/appointments/preferred-slots?time=${encodeURIComponent(preferredTime)}`
  );
  
  if (response.ok()) {
    return await response.json();
  }
  
  return [];
}

/**
 * Verify FIFO priority for preferred slots
 */
export async function verifyFIFOPriority(
  page: Page,
  preferredTime: string
): Promise<boolean> {
  const registrations = await getPreferredSlotRegistrations(page, preferredTime);
  
  if (registrations.length < 2) return true;
  
  // Verify they are sorted by registration time
  for (let i = 1; i < registrations.length; i++) {
    const prev = new Date(registrations[i - 1].registered_at);
    const curr = new Date(registrations[i].registered_at);
    
    if (curr < prev) {
      return false;
    }
  }
  
  return true;
}
