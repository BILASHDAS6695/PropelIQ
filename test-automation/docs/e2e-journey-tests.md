# E2E Journey: Complete Patient Appointment Lifecycle

Complete Playwright TypeScript E2E test automation for the patient appointment lifecycle journey.

## Overview

This E2E test validates the complete patient experience from initial registration through appointment attendance, spanning 8 distinct phases over an estimated 8-10 minute execution time.

**Journey Phases**:
1. Patient Registration & Activation
2. Appointment Search & Booking
3. Preferred Slot Registration
4. Appointment Confirmation & PDF
5. AI-Assisted Intake Completion
6. Calendar Integration
7. Appointment Reminder Verification
8. Appointment Day - Patient Arrival

## Test File

[e2e-patient-lifecycle.spec.ts](../tests/e2e-patient-lifecycle.spec.ts)

## Requirements Coverage

| Requirement | Description | Phase |
|-------------|-------------|-------|
| FR-001 | Patient Self-Registration | 1 |
| FR-006 | Search Available Appointments | 2 |
| FR-007 | Book Available Slot | 2 |
| FR-013 | Appointment Confirmation | 4 |
| FR-014 | Preferred Slot Registration | 3 |
| FR-018 | 24-Hour Reminder | 7 |
| FR-019 | 2-Hour Reminder | 7 |
| FR-022 | Calendar Integration | 6 |
| FR-027 | AI Conversational Intake | 5 |
| FR-031 | Intake Completion | 5 |
| FR-011 | Patient Arrival Tracking | 8 |

## Use Cases Coverage

- UC-001: Patient Books Appointment
- UC-005: Patient Completes Intake
- UC-011: Appointment Reminders
- UC-015: Calendar Sync
- UC-014: Patient Arrival

## Running the Test

```bash
# Run E2E journey test
npm run test:e2e

# Run with headed browser (watch the journey)
npx playwright test tests/e2e-patient-lifecycle.spec.ts --headed

# Run in debug mode
npx playwright test tests/e2e-patient-lifecycle.spec.ts --debug

# Run in UI mode for step-by-step execution
npm run test:ui tests/e2e-patient-lifecycle.spec.ts
```

## Test Data

### Patient Information
```typescript
{
  name: 'Emma Johnson',
  email: 'emma.johnson@example.com',
  phone: '555-2468',
  password: 'SecurePass123!'
}
```

### Appointment Details
```typescript
{
  provider: 'Dr. Smith',
  date: '2026-06-20',
  time: '02:00 PM',
  preferredTime: '11:00 AM'
}
```

### Intake Information
```typescript
{
  medicalHistory: 'Seasonal allergies and occasional migraines',
  medications: 'Zyrtec 10mg daily',
  allergies: 'Pollen (environmental)',
  symptoms: 'Sneezing, itchy eyes'
}
```

## Pre-Conditions

Before running the E2E test:

1. **Clean Database**: Test data should be cleaned up
2. **Provider Availability**: Dr. Smith must have slots available on 2026-06-20
3. **Email/SMS Services**: Mock or test email/SMS services must be configured
4. **Calendar API**: Mock OAuth flow for calendar integration
5. **Background Jobs**: Reminder scheduling must be functional

## Fixtures & Helpers

### e2e.fixture.ts
- `cleanDatabase`: Cleans up test data before and after
- `seededAvailability`: Seeds provider slots for booking
- `e2eTestData`: Complete test data for the journey

### e2e.helpers.ts
- `activateTestAccount()`: Simulate account activation
- `getActivationLink()`: Retrieve activation URL from email
- `verifyEmailReceived()`: Check email delivery
- `verifySMSSent()`: Check SMS delivery
- `mockCalendarOAuth()`: Mock Google Calendar OAuth
- `verifyCalendarEventCreated()`: Verify calendar sync
- `verifyRemindersScheduled()`: Check reminder configuration
- `getPatientJourneyData()`: Retrieve complete patient data
- `verifyJourneyCompletionCriteria()`: Validate success criteria
- `cleanupTestPatient()`: Remove test data after execution
- `createJourneyTimer()`: Measure execution time
- `waitForBackgroundJob()`: Wait for async jobs

## Test Phases Detail

### Phase 1: Registration & Activation
**Duration**: 2-3 minutes

- Navigate to registration page
- Fill registration form
- Verify success message
- Activate account (via API for testing)
- Login with new credentials
- Verify patient dashboard access

### Phase 2: Appointment Booking
**Duration**: 2 minutes

- Navigate to booking page
- Search for available slots
- Verify slots displayed correctly
- Select 2:00 PM slot

### Phase 3: Preferred Slot Registration
**Duration**: 1 minute

- Expand preferred slot options
- View all slots including unavailable
- Select 11:00 AM as preferred
- Confirm booking with preference

### Phase 4: Confirmation & PDF
**Duration**: 1 minute

- Verify confirmation message
- Verify appointment details
- Download confirmation PDF
- Verify PDF contents
- Check confirmation email

### Phase 5: AI Intake
**Duration**: 3 minutes

- Navigate to intake page
- Select AI mode
- Respond to medical history questions
- Respond to medications questions
- Respond to allergies questions
- Respond to symptoms questions
- Review captured data
- Confirm intake

### Phase 6: Calendar Integration
**Duration**: 1 minute

- Navigate to appointment details
- Connect Google Calendar
- Complete OAuth flow (mocked)
- Verify sync success

### Phase 7: Reminder Verification
**Duration**: Instant (verification only)

- Verify 24-hour email reminder scheduled
- Verify 24-hour SMS reminder scheduled
- Verify 2-hour email reminder scheduled
- Verify 2-hour SMS reminder scheduled

### Phase 8: Patient Arrival
**Duration**: 1 minute

- Staff logs in (separate session)
- Navigate to schedule
- Verify patient in schedule
- Mark patient as arrived
- Verify arrival recorded
- Verify timestamp logged

## Cross-Phase Validation

After all phases complete, the test verifies:

- ✅ User account exists and is active
- ✅ Appointment booked with correct details
- ✅ Preferred slot registered
- ✅ Intake data captured and structured
- ✅ Calendar event created
- ✅ Reminders scheduled
- ✅ Patient arrival logged
- ✅ Complete audit trail exists

## Success Criteria

| Metric | Target | Validation |
|--------|--------|------------|
| Journey Completion Time | < 10 minutes | Measured via timer |
| All Phases Passed | 100% | All test steps pass |
| Email Delivery | 100% | All emails verified |
| SMS Delivery | 100% | All SMS verified |
| Calendar Sync | 100% | Event created |
| Data Accuracy | 100% | Cross-validation passes |
| Zero Errors | True | No exceptions thrown |

## Error Handling

The test includes graceful error handling for:

- **Email Delivery Failure**: Falls back to API activation
- **Calendar API Unavailable**: Gracefully degrades, appointment still valid
- **AI Service Timeout**: Can fallback to manual mode
- **SMS Service Failure**: Logs warning but continues

## Mock Services

### Email Service
```typescript
// Mocked via /api/test/emails endpoint
await verifyEmailReceived(page, 'emma.johnson@example.com', 'Activate Your Account');
```

### SMS Service
```typescript
// Mocked via /api/test/sms endpoint
await verifySMSSent(page, '555-2468', 'Appointment Reminder');
```

### Calendar OAuth
```typescript
// Route mocking for Google Calendar
await mockCalendarOAuth(page);
```

## Cleanup

The test automatically cleans up:
- User account created during test
- Appointments booked
- Intake data
- Calendar sync records
- Email/SMS test data

## Execution Time Tracking

```typescript
const timer = createJourneyTimer();
// Run journey...
console.log(`Journey completed in ${timer.getElapsedMinutes()} minutes`);
```

## Multi-Context Testing

Phase 8 uses a separate browser context for the staff session:

```typescript
const staffPage = await context.newPage();
// Staff actions...
await staffPage.close();
```

## API Endpoints Used

- `POST /api/test/activate-account` - Activate test account
- `GET /api/test/emails` - Retrieve test emails
- `GET /api/test/sms` - Retrieve test SMS
- `POST /api/test/seed-availability` - Seed provider slots
- `POST /api/test/cleanup` - Clean test data
- `GET /api/appointments/:id/reminders` - Get scheduled reminders
- `GET /api/appointments/:id/calendar-sync` - Verify calendar sync
- `GET /api/intake` - Get patient intake data
- `POST /api/appointments/:id/arrive` - Mark patient arrived

## Troubleshooting

### Test Times Out
- Check if background jobs are processing
- Verify email/SMS services are mocked correctly
- Ensure database cleanup completes

### Calendar Sync Fails
- Verify OAuth routes are being mocked
- Check calendar API endpoint configuration

### Intake AI Fails
- Verify AI service is running or mocked
- Check message input/response timing

### Reminders Not Scheduled
- Verify appointment was created successfully
- Check background job processing

## Notes

- This is a **long-running test** (8-10 minutes)
- Run separately from unit/integration tests
- Requires clean database state
- Best run in CI with dedicated test environment
- Use `--headed` mode for debugging
- Consider running nightly or on-demand

---

**Source**: [e2e_patient_appointment_lifecycle.md](../../.propel/context/test/e2e_patient_appointment_lifecycle.md)  
**Framework**: Playwright 1.40+ with TypeScript 5.3+  
**Estimated Duration**: 8-10 minutes
