# Appointment Booking & Management - Test Automation

Complete Playwright TypeScript test automation suite for Appointment Booking & Management feature.

## Overview

This test suite validates:
- Patient searches and books appointments (FR-006, FR-007)
- Staff books appointments for patients (FR-008)
- Walk-in registrations and same-day queue (FR-009, FR-010)
- Patient arrival tracking (FR-011)
- Preferred slot swap functionality (FR-014 to FR-017)
- Appointment confirmations (FR-013)

## Test Files

### Happy Path
[appointment-happy-path.spec.ts](../tests/appointment-happy-path.spec.ts)
- TW-APPT-001: Patient Searches and Books Available Slot
- TW-APPT-002: Patient Books With Preferred Slot Selection
- TW-APPT-003: Staff Books Appointment for Patient
- TW-APPT-004: Staff Registers Walk-In and Marks Arrived
- TW-APPT-005: Automatic Preferred Slot Swap

### Edge Cases
[appointment-edge-cases.spec.ts](../tests/appointment-edge-cases.spec.ts)
- TW-APPT-006: Concurrent Booking Attempts (Race Condition)
- TW-APPT-007: Multiple Patients Prefer Same Slot - FIFO Priority

### Error Scenarios
[appointment-error-scenarios.spec.ts](../tests/appointment-error-scenarios.spec.ts)
- TW-APPT-008: Patient Attempts Self-Check-In (Forbidden)
- TW-APPT-009: Booking With No Available Slots

## Page Objects

- **AppointmentSearchPage**: Search and book appointments
- **AppointmentConfirmationPage**: View confirmation details
- **StaffBookingPage**: Staff booking for patients
- **WalkInPage**: Walk-in registration and queue management
- **PatientDashboardPage**: Patient's view of appointments

## Fixtures & Helpers

### appointment.fixture.ts
- `providers`: Provider test data (Dr. Smith, Dr. Jones)
- `appointmentTestData`: Standard booking scenarios
- `testPatientsWithPreferences`: Patients with preferred slots
- `authenticatedPatientPage`: Pre-authenticated patient context
- `authenticatedStaffPage`: Pre-authenticated staff context

### appointment.helpers.ts
- `seedProviderAvailability()`: Set up available slots
- `clearAppointmentsForDate()`: Clean up test data
- `createAppointment()`: Create appointment via API
- `cancelAppointment()`: Cancel appointment via API
- `verifySlotAvailability()`: Check if slot is available
- `getPatientAppointments()`: Retrieve patient appointments
- `verifyAppointmentExists()`: Verify appointment in system
- `mockEmailService()`: Mock appointment confirmation emails
- `mockSMSService()`: Mock SMS notifications
- `verifyEmailSent()`: Check email delivery
- `verifySMSSent()`: Check SMS delivery
- `waitForSlotSwap()`: Wait for preferred slot swap processing
- `registerPreferredSlot()`: Register preferred slot preference
- `getSameDayQueue()`: Get walk-in queue
- `markPatientArrived()`: Mark patient arrival
- `verifyPDFGenerated()`: Check PDF generation
- `createWalkInAppointment()`: Create walk-in via API
- `verifyConcurrentBookingProtection()`: Verify race condition handling
- `getPreferredSlotRegistrations()`: Get all preferences for a slot
- `verifyFIFOPriority()`: Verify first-in-first-out ordering

## Running Tests

```bash
# Run all appointment tests
npm run test:appointments

# Run specific test file
npx playwright test tests/appointment-happy-path.spec.ts

# Run in UI mode
npm run test:ui tests/appointment-happy-path.spec.ts

# Debug specific test
npx playwright test tests/appointment-edge-cases.spec.ts --debug
```

## Concurrent Booking Test

The concurrent booking test (TW-APPT-006) uses separate browser contexts to simulate race conditions. This ensures only one patient can book the same slot.

## Preferred Slot Swap Testing

Tests TW-APPT-005 and TW-APPT-007 validate the automatic slot swap functionality:
- When a preferred slot becomes available, the system automatically swaps
- Multiple preferences are handled with FIFO priority
- Notifications are sent via email and SMS

## Walk-In Flow

Test TW-APPT-004 validates the complete walk-in workflow:
1. Staff searches for patient
2. Creates new patient if not found
3. Adds to same-day queue
4. Marks patient as arrived

## API Endpoints Used

- `GET /api/appointments` - Get patient appointments
- `POST /api/appointments` - Create appointment
- `DELETE /api/appointments/:id` - Cancel appointment
- `GET /api/slots` - Check slot availability
- `POST /api/admin/provider-availability` - Seed provider slots
- `DELETE /api/admin/appointments` - Clear test data
- `PUT /api/appointments/:id/preferred-slot` - Register preference
- `POST /api/appointments/:id/arrive` - Mark arrival
- `GET /api/appointments/:id/pdf` - Get confirmation PDF
- `POST /api/appointments/walk-in` - Create walk-in
- `GET /api/appointments/same-day-queue` - Get walk-in queue
- `GET /api/appointments/preferred-slots` - Get slot preferences
- `POST /api/appointments/:id/check-in` - Patient check-in (forbidden)

## Data Setup

### Prerequisites
Before running tests, ensure:
- Provider availability is seeded for test dates
- Test patients exist in the system
- Email and SMS services are mocked or configured

### Test Data Seeding
Use helper functions to seed test data:

```typescript
import { seedProviderAvailability } from '../helpers/appointment.helpers';

test.beforeEach(async ({ page }) => {
  await seedProviderAvailability(page, 'Dr. Smith', '2026-06-15', [
    '09:00 AM',
    '10:00 AM',
    '11:00 AM',
    '02:00 PM',
    '03:00 PM',
    '04:00 PM',
  ]);
});
```

### Cleanup
Tests should clean up created appointments:

```typescript
import { clearAppointmentsForDate } from '../helpers/appointment.helpers';

test.afterEach(async ({ page }) => {
  await clearAppointmentsForDate(page, '2026-06-15');
});
```

## Traceability

| Test Case | Requirements | Use Cases | Priority |
|-----------|--------------|-----------|----------|
| TW-APPT-001 | FR-006, FR-007, FR-013 | UC-001 | Critical |
| TW-APPT-002 | FR-014 | UC-001 | High |
| TW-APPT-003 | FR-008 | UC-002 | Critical |
| TW-APPT-004 | FR-009, FR-010, FR-011 | UC-003, UC-014 | High |
| TW-APPT-005 | FR-015, FR-016, FR-017 | UC-004 | High |
| TW-APPT-006 | FR-007 | - | High |
| TW-APPT-007 | FR-015 | UC-004 | Medium |
| TW-APPT-008 | FR-012 | - | High |
| TW-APPT-009 | FR-006 | UC-001 | Medium |

## Notes

- Use database seeding for provider availability
- Mock email and SMS services for notification verification
- Implement wait strategies for asynchronous slot swap processing
- Test concurrent scenarios with separate browser contexts
- Clear appointment data between test runs
- Preferred slot swap may take 2-5 seconds to process
- FIFO priority is based on `registered_at` timestamp

## Special Considerations

### Preferred Slot Swap Timing
The automatic slot swap happens asynchronously. Tests include a 2-second wait for processing. In production, this may vary based on system load.

### Concurrent Booking Protection
The system uses database-level locking to prevent double booking. Tests verify that only one of two concurrent booking attempts succeeds.

### Walk-In Queue Management
Walk-ins are added to a FIFO queue based on arrival timestamp. Staff can mark patients as arrived from this queue.

---

**Source**: [tw_appointments.md](../../.propel/context/test/tw_appointments.md)  
**Framework**: Playwright 1.40+ with TypeScript 5.3+
