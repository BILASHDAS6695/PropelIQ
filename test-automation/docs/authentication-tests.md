# Authentication & Access Control - Test Automation

Complete Playwright TypeScript test automation suite for Authentication & Access Control feature.

## Overview

This test suite validates:
- Patient self-registration (FR-001)
- Admin user management (FR-002)
- Role-based access control (FR-003)
- Session timeout (FR-004, NFR-006)
- Secure credential storage (FR-005)
- User deactivation (FR-049)

## Test Files

### Happy Path
[authentication-happy-path.spec.ts](../tests/authentication-happy-path.spec.ts)
- TW-AUTH-001: Patient Self-Registration Success
- TW-AUTH-002: Admin Creates Staff Account
- TW-AUTH-003: RBAC - Patient Cannot Access Admin Endpoints
- TW-AUTH-004: Session Expires After 15 Minutes Inactivity

### Edge Cases
[authentication-edge-cases.spec.ts](../tests/authentication-edge-cases.spec.ts)
- TW-AUTH-005: Registration Fails With Duplicate Email
- TW-AUTH-006: Deactivated User Access Revoked Immediately

### Error Scenarios
[authentication-error-scenarios.spec.ts](../tests/authentication-error-scenarios.spec.ts)
- TW-AUTH-007: Registration Rejected - Weak Password
- TW-AUTH-008: Login Fails With Wrong Password
- TW-AUTH-009: Patient Cannot Create User Accounts

## Page Objects

- **LoginPage**: User authentication
- **RegistrationPage**: Patient self-registration
- **AdminUserManagementPage**: Admin user creation and management
- **DashboardPage**: Role-specific dashboards

## Fixtures & Helpers

### auth.fixture.ts
- `testUsers`: Predefined test user credentials
- `registrationTestData`: Valid and invalid registration data
- `authenticatedPatientPage`: Pre-authenticated patient context
- `authenticatedStaffPage`: Pre-authenticated staff context
- `authenticatedAdminPage`: Pre-authenticated admin context

### auth.helpers.ts
- `login()`: Login helper
- `logout()`: Logout helper
- `getAuthToken()`: Extract JWT from localStorage
- `getAuthCookie()`: Extract auth cookie
- `clearAuth()`: Clear authentication state
- `makeAuthenticatedRequest()`: API request with auth
- `verifyUserRole()`: Check user role via API
- `verifyUserExists()`: Verify user in database
- `createTestUser()`: Create test user via API
- `deleteTestUser()`: Delete test user via API
- `verifyAuditLog()`: Check audit log entries
- `mockEmailService()`: Mock email sending
- `verifyEmailSent()`: Verify email was sent
- `mockClock()`: Mock system clock for session timeout tests
- `advanceTime()`: Advance mocked time

## Running Tests

```bash
# Run all authentication tests
npm run test:auth

# Run specific test file
npx playwright test tests/authentication-happy-path.spec.ts

# Run in UI mode
npm run test:ui tests/authentication-happy-path.spec.ts

# Debug specific test
npx playwright test tests/authentication-happy-path.spec.ts --debug
```

## Session Timeout Testing

The session timeout test (TW-AUTH-004) includes a 16-minute wait. For faster testing:

1. **Option 1**: Use clock mocking (recommended)
   - Import `mockClock()` and `advanceTime()` from auth.helpers.ts
   - Replace `page.waitForTimeout()` with `advanceTime()`

2. **Option 2**: Reduce timeout in backend
   - Set session timeout to 1-2 minutes for testing
   - Update test wait times accordingly

3. **Option 3**: Skip in CI, run manually
   - Tag test with `@slow`
   - Exclude from CI pipelines

## API Endpoints Used

- `GET /api/users?email={email}` - Get user by email
- `GET /api/users/count?email={email}` - Count users by email
- `POST /api/admin/users` - Create user (admin only)
- `DELETE /api/admin/users?email={email}` - Delete user (admin only)
- `GET /api/admin/audit-logs?action={action}` - Get audit logs
- `GET /api/admin/emails?to={email}&subject={subject}` - Check sent emails

## Data Setup

### Prerequisites
Before running tests, ensure these test users exist:
- `patient@example.com` (role: Patient)
- `staff@clinic.com` (role: Staff)
- `admin@clinic.com` (role: Admin)
- `existing@example.com` (role: Patient)
- `user@example.com` (role: Patient)

### Cleanup
Tests should clean up created data after execution. Use hooks:

```typescript
test.afterEach(async ({ page }) => {
  // Delete test users created during test
  await deleteTestUser(page, 'john.doe@example.com');
  await deleteTestUser(page, 'jane.smith@clinic.com');
});
```

## Traceability

| Test Case | Requirements | Use Cases | Priority |
|-----------|--------------|-----------|----------|
| TW-AUTH-001 | FR-001 | UC-012 | Critical |
| TW-AUTH-002 | FR-002 | UC-012 | Critical |
| TW-AUTH-003 | FR-003 | - | Critical |
| TW-AUTH-004 | FR-004, NFR-006 | - | High |
| TW-AUTH-005 | FR-001 | - | High |
| TW-AUTH-006 | FR-049 | UC-012 | High |
| TW-AUTH-007 | FR-001, FR-005 | - | Medium |
| TW-AUTH-008 | FR-005 | - | High |
| TW-AUTH-009 | FR-002, FR-003 | - | High |

## Notes

- Run authentication tests in isolation to avoid session conflicts
- Clear cookies and localStorage between tests
- Use separate database instances for concurrent test runs
- Mock email service for activation email verification
- Consider using fixtures for pre-authenticated contexts
- Session timeout test requires 16 minutes - use clock mocking or skip in CI

---

**Source**: [tw_authentication.md](../../.propel/context/test/tw_authentication.md)  
**Framework**: Playwright 1.40+ with TypeScript 5.3+
