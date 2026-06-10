# Playwright Test Automation - Patient Intake

This directory contains Playwright TypeScript test automation scripts for the Patient Intake feature.

## Structure

```
test-automation/
├── pages/                          # Page Object Models
│   ├── login.page.ts               # Login page
│   ├── registration.page.ts        # Patient registration
│   ├── admin-user-management.page.ts  # Admin user management
│   ├── dashboard.page.ts           # Role-specific dashboards
│   ├── intake-landing.page.ts      # Intake mode selection
│   ├── ai-conversational-intake.page.ts  # AI intake flow
│   ├── manual-form-intake.page.ts  # Manual form intake
│   ├── intake-summary.page.ts      # Intake summary/edit
│   ├── appointment-search.page.ts  # Appointment search/booking
│   ├── appointment-confirmation.page.ts  # Appointment confirmation
│   ├── staff-booking.page.ts       # Staff appointment booking
│   ├── walk-in.page.ts             # Walk-in registration
│   ├── patient-dashboard.page.ts   # Patient dashboard
│   └── index.ts                    # Page exports
├── tests/                          # Test specifications
│   ├── authentication-happy-path.spec.ts      # Auth happy path scenarios
│   ├── authentication-edge-cases.spec.ts      # Auth edge case scenarios
│   ├── authentication-error-scenarios.spec.ts # Auth error scenarios
│   ├── patient-intake-happy-path.spec.ts      # Intake happy path scenarios
│   ├── patient-intake-edge-cases.spec.ts      # Intake edge case scenarios
│   ├── patient-intake-error-scenarios.spec.ts # Intake error scenarios
│   ├── appointment-happy-path.spec.ts         # Appointment happy path scenarios
│   ├── appointment-edge-cases.spec.ts         # Appointment edge case scenarios
│   ├── appointment-error-scenarios.spec.ts    # Appointment error scenarios
│   └── e2e-patient-lifecycle.spec.ts          # E2E patient journey test
├── fixtures/                       # Test fixtures
│   ├── auth.fixture.ts             # Auth test data and authenticated fixtures
│   ├── intake.fixture.ts           # Intake test data and authenticated fixtures
│   ├── appointment.fixture.ts      # Appointment test data and fixtures
│   └── e2e.fixture.ts              # E2E journey test data and fixtures
├── helpers/                        # Test utilities
│   ├── auth.helpers.ts             # Authentication helpers, API utilities
│   ├── intake.helpers.ts           # Intake helpers, API utilities, mocks
│   ├── appointment.helpers.ts      # Appointment helpers, API utilities
│   └── e2e.helpers.ts              # E2E journey helpers, validation utilities
├── docs/                           # Documentation
│   ├── authentication-tests.md     # Auth test documentation
│   └── appointment-tests.md        # Appointment test documentation
├── playwright.config.ts            # Playwright configuration
├── package.json                    # Dependencies
├── tsconfig.json                   # TypeScript configuration
└── README.md                       # This file
```

## Setup

1. Install dependencies:
```bash
cd test-automation
npm install
```

2. Install Playwright browsers:
```bash
npx playwright install
```

## Running Tests

### Run all tests
```bash
npm test
```

### Run intake tests only
```bash
npm run test:intake
```

### Run authentication tests only
```bash
npm run test:auth
```

### Run appointment tests only
```bash
npm run test:appointments
```

### Run E2E journey tests
```bash
npm run test:e2e
```

### Run with UI mode
```bash
npm run test:ui
```

### Run in headed mode (see browser)
```bash
npm run test:headed
```

### Debug tests
```bash
npm run test:debug
```

### View test report
```bash
npm run test:report
```

## Test Coverage

### Authentication & Access Control (9 tests)

**Happy Path (4 tests)**
- **TW-AUTH-001**: Patient Self-Registration Success
- **TW-AUTH-002**: Admin Creates Staff Account
- **TW-AUTH-003**: RBAC - Patient Cannot Access Admin Endpoints
- **TW-AUTH-004**: Session Expires After 15 Minutes Inactivity

**Edge Cases (2 tests)**
- **TW-AUTH-005**: Registration Fails With Duplicate Email
- **TW-AUTH-006**: Deactivated User Access Revoked Immediately

**Error Scenarios (3 tests)**
- **TW-AUTH-007**: Registration Rejected - Weak Password
- **TW-AUTH-008**: Login Fails With Wrong Password
- **TW-AUTH-009**: Patient Cannot Create User Accounts

**Coverage**: FR-001 to FR-005, FR-049, NFR-006

---

### Patient Intake (8 tests)

**Happy Path (4 tests)**
- **TW-INTAKE-001**: Complete AI Conversational Intake
- **TW-INTAKE-002**: Complete Manual Form Intake
- **TW-INTAKE-003**: Switch From AI to Manual Mode Mid-Process
- **TW-INTAKE-004**: Patient Edits Submitted Intake

**Edge Cases (2 tests)**
- **TW-INTAKE-005**: Switch From Manual to AI Mode
- **TW-INTAKE-006**: Multiple Edit Cycles

**Error Scenarios (2 tests)**
- **TW-INTAKE-007**: Submit Manual Form With Missing Required Fields
- **TW-INTAKE-008**: AI Parsing Ambiguous Response

**Coverage**: FR-027 to FR-031

---

### Appointment Booking & Management (9 tests)

**Happy Path (5 tests)**
- **TW-APPT-001**: Patient Searches and Books Available Slot
- **TW-APPT-002**: Patient Books With Preferred Slot Selection
- **TW-APPT-003**: Staff Books Appointment for Patient
- **TW-APPT-004**: Staff Registers Walk-In and Marks Arrived
- **TW-APPT-005**: Automatic Preferred Slot Swap

**Edge Cases (2 tests)**
- **TW-APPT-006**: Concurrent Booking Attempts (Race Condition)
- **TW-APPT-007**: Multiple Patients Prefer Same Slot - FIFO Priority

**Error Scenarios (2 tests)**
- **TW-APPT-008**: Patient Attempts Self-Check-In (Forbidden)
- **TW-APPT-009**: Booking With No Available Slots

**Coverage**: FR-006 to FR-017, UC-001 to UC-004, UC-014

---

---

### E2E Journey Tests (1 comprehensive test)

**Complete Patient Appointment Lifecycle**
- **E2E-LIFECYCLE-001**: Registration → Booking → Intake → Calendar → Reminders → Arrival
  - 8 phases, 8-10 minute execution time
  - Cross-phase validation
  - Complete data integrity verification

**Coverage**: FR-001, FR-006, FR-007, FR-011, FR-013, FR-014, FR-018, FR-019, FR-022, FR-027, FR-031

---

**Total**: 27 test cases across 3 feature areas + 1 comprehensive E2E journey

## Page Object Model Pattern

Tests follow the Page Object Model (POM) pattern for maintainability:

```typescript
import { IntakeLandingPage, AIConversationalIntakePage } from '../pages';

test('example', async ({ page }) => {
  const intakeLanding = new IntakeLandingPage(page);
  const aiIntake = new AIConversationalIntakePage(page);
  
  await intakeLanding.goto();
  await intakeLanding.selectAIMode();
  await aiIntake.sendMessage('I have diabetes');
});
```

## Configuration

- **Base URL**: `http://localhost:4200` (configurable via `BASE_URL` env var)
- **Browsers**: Chromium, Firefox, WebKit, Mobile Chrome, Mobile Safari
- **Retries**: 2 on CI, 0 locally
- **Reporters**: HTML, JSON, JUnit
- **Screenshots**: On failure only
- **Videos**: Retained on failure
- **Traces**: On first retry

## Environment Variables

```bash
# Set base URL
BASE_URL=https://staging.example.com npm test

# Run in CI mode
CI=true npm test
```

## Best Practices

1. **Use data-testid selectors**: All selectors use `data-testid` attributes
2. **Page Objects**: Encapsulate page interactions in POM classes
3. **Fixtures**: Use fixtures for authentication and test data
4. **Helpers**: Extract common utilities to helper functions
5. **API Validation**: Verify data persistence via API calls
6. **Explicit Waits**: Use `waitForSelector` for dynamic content
7. **Isolated Tests**: Each test is independent and can run in any order

## CI/CD Integration

Example GitHub Actions workflow:

```yaml
- name: Install dependencies
  run: |
    cd test-automation
    npm ci
    npx playwright install --with-deps

- name: Run Playwright tests
  run: cd test-automation && npm test
  env:
    CI: true
    BASE_URL: ${{ secrets.TEST_BASE_URL }}

- name: Upload test results
  uses: actions/upload-artifact@v3
  if: always()
  with:
    name: playwright-report
    path: test-automation/playwright-report/
```

## Troubleshooting

### Tests fail with "Timeout waiting for selector"
- Check that the application is running on the configured `BASE_URL`
- Verify `data-testid` attributes match the selectors
- Increase timeout in `playwright.config.ts` if needed

### Authentication failures
- Ensure test users exist in the database
- Check credentials in `fixtures/intake.fixture.ts`
- Verify JWT token handling in helpers

### AI service mock not working
- Check route patterns in `helpers/intake.helpers.ts`
- Ensure `mockAIService()` is called before test starts

## Contributing

1. Follow TypeScript strict mode
2. Add JSDoc comments to page objects and helpers
3. Use meaningful test names that describe behavior
4. Update README when adding new test files

## E2E Journey Tests

E2E tests validate complete user journeys across multiple features. These tests take longer to execute (8-10 minutes) but provide comprehensive validation of the entire system.

**Important**: E2E tests require:
- Clean database state
- Email/SMS mocking or test services
- Calendar API mocking
- Seeded provider availability

```bash
# Run E2E tests with full setup
npm run test:e2e
```

---

**Generated**: 2026-06-10  
**Source**: tw_authentication.md, tw_patient_intake.md, tw_appointments.md, e2e_patient_appointment_lifecycle.md  
**Framework**: Playwright 1.40+ with TypeScript 5.3+
