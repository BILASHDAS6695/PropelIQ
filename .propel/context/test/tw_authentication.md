# Test Workflow: Authentication & Access Control

## Metadata

| Field | Value |
|-------|-------|
| **Feature** | Authentication & Access Control |
| **Test Type** | Feature-level |
| **Requirements** | FR-001 to FR-005 |
| **Use Cases** | UC-012 (Admin User Management) |
| **Priority** | Critical |
| **Test Framework** | Playwright + TypeScript |

---

## Feature Overview

This test workflow validates the authentication and access control system including:
- Patient self-registration (FR-001)
- Admin user management (FR-002)
- Role-based access control (FR-003)
- Session timeout (FR-004)
- Secure credential storage (FR-005)

---

## Page Objects Required

```yaml
pages:
  - LoginPage:
      selectors:
        emailInput: '[data-testid="email-input"]'
        passwordInput: '[data-testid="password-input"]'
        submitButton: '[data-testid="login-button"]'
        errorMessage: '[data-testid="error-message"]'
      
  - RegistrationPage:
      selectors:
        nameInput: '[data-testid="name-input"]'
        emailInput: '[data-testid="email-input"]'
        phoneInput: '[data-testid="phone-input"]'
        passwordInput: '[data-testid="password-input"]'
        confirmPasswordInput: '[data-testid="confirm-password-input"]'
        submitButton: '[data-testid="register-button"]'
        successMessage: '[data-testid="success-message"]'
        
  - AdminUserManagementPage:
      selectors:
        createUserButton: '[data-testid="create-user-button"]'
        userSearchInput: '[data-testid="user-search"]'
        userTable: '[data-testid="user-table"]'
        roleDropdown: '[data-testid="role-select"]'
        deactivateButton: '[data-testid="deactivate-user"]'
        
  - DashboardPage:
      selectors:
        patientDashboard: '[data-testid="patient-dashboard"]'
        staffDashboard: '[data-testid="staff-dashboard"]'
        adminDashboard: '[data-testid="admin-dashboard"]'
        logoutButton: '[data-testid="logout-button"]'
```

---

## Test Cases

### Happy Path

#### TW-AUTH-001: Patient Self-Registration Success

**Requirement**: FR-001  
**Use Case**: UC-012 precondition  
**Priority**: Critical

```yaml
test: "Patient Self-Registration Success"
steps:
  - step: "Navigate to registration page"
    action: goto
    url: "/register"
    
  - step: "Fill in registration form"
    actions:
      - fill:
          selector: '[data-testid="name-input"]'
          value: "John Doe"
      - fill:
          selector: '[data-testid="email-input"]'
          value: "john.doe@example.com"
      - fill:
          selector: '[data-testid="phone-input"]'
          value: "555-0123"
      - fill:
          selector: '[data-testid="password-input"]'
          value: "SecurePass123!"
      - fill:
          selector: '[data-testid="confirm-password-input"]'
          value: "SecurePass123!"
          
  - step: "Submit registration"
    action: click
    selector: '[data-testid="register-button"]'
    
  - step: "Verify success message"
    action: expect
    selector: '[data-testid="success-message"]'
    assertion: toBeVisible
    
  - step: "Verify activation email sent"
    action: checkEmail
    to: "john.doe@example.com"
    subject: "Activate Your Account"
    
  - step: "Login with new credentials"
    actions:
      - goto: "/login"
      - fill:
          selector: '[data-testid="email-input"]'
          value: "john.doe@example.com"
      - fill:
          selector: '[data-testid="password-input"]'
          value: "SecurePass123!"
      - click: '[data-testid="login-button"]'
          
  - step: "Verify patient dashboard accessible"
    action: expect
    selector: '[data-testid="patient-dashboard"]'
    assertion: toBeVisible

checkpoints:
  - name: "Account Created"
    verify: "User record exists in database"
  - name: "Email Sent"
    verify: "Activation email in outbox"
  - name: "Login Successful"
    verify: "JWT token issued"
```

---

#### TW-AUTH-002: Admin Creates Staff Account

**Requirement**: FR-002  
**Use Case**: UC-012  
**Priority**: Critical

```yaml
test: "Admin Creates Staff Account"
preconditions:
  - authenticated_as: "Admin"
  
steps:
  - step: "Navigate to user management"
    action: goto
    url: "/admin/users"
    
  - step: "Click create user button"
    action: click
    selector: '[data-testid="create-user-button"]'
    
  - step: "Fill staff user details"
    actions:
      - fill:
          selector: '[data-testid="name-input"]'
          value: "Jane Smith"
      - fill:
          selector: '[data-testid="email-input"]'
          value: "jane.smith@clinic.com"
      - select:
          selector: '[data-testid="role-select"]'
          value: "Staff"
          
  - step: "Submit user creation"
    action: click
    selector: '[data-testid="submit-user-button"]'
    
  - step: "Verify user appears in table"
    action: expect
    selector: '[data-testid="user-table"] >> text=Jane Smith'
    assertion: toBeVisible
    
  - step: "Verify role is Staff"
    action: expect
    selector: '[data-testid="user-table"] >> text=Staff'
    assertion: toBeVisible
    
  - step: "Verify activation email sent to new staff"
    action: checkEmail
    to: "jane.smith@clinic.com"
    subject: "Welcome to HealthPlatform - Activate Your Staff Account"

checkpoints:
  - name: "Staff Account Created"
    verify: "User exists with role=Staff"
  - name: "Audit Log Entry"
    verify: "Admin action logged"
```

---

#### TW-AUTH-003: Role-Based Access Control Enforcement

**Requirement**: FR-003  
**Priority**: Critical

```yaml
test: "RBAC - Patient Cannot Access Admin Endpoints"
preconditions:
  - authenticated_as: "Patient"
  
steps:
  - step: "Attempt to access admin dashboard"
    action: goto
    url: "/admin/dashboard"
    
  - step: "Verify access denied"
    action: expect
    selector: '[data-testid="error-message"]'
    assertion: toContainText
    text: "403 Forbidden"
    
  - step: "Attempt to access staff endpoints"
    action: goto
    url: "/staff/queue"
    
  - step: "Verify access denied"
    action: expect
    selector: '[data-testid="error-message"]'
    assertion: toContainText
    text: "403 Forbidden"

checkpoints:
  - name: "Authorization Enforced"
    verify: "HTTP 403 returned for unauthorized access"
```

---

#### TW-AUTH-004: Session Timeout After 15 Minutes

**Requirement**: FR-004, NFR-006  
**Priority**: High

```yaml
test: "Session Expires After 15 Minutes Inactivity"
preconditions:
  - authenticated_as: "Patient"
  
steps:
  - step: "Verify initial authenticated state"
    action: expect
    selector: '[data-testid="patient-dashboard"]'
    assertion: toBeVisible
    
  - step: "Perform action at T+0"
    action: click
    selector: '[data-testid="appointments-link"]'
    
  - step: "Wait 14 minutes"
    action: wait
    duration: 840000  # 14 minutes in ms
    
  - step: "Perform action at T+14min (within timeout)"
    action: click
    selector: '[data-testid="profile-link"]'
    
  - step: "Verify action succeeds"
    action: expect
    selector: '[data-testid="profile-page"]'
    assertion: toBeVisible
    
  - step: "Wait additional 2 minutes (total 16 min)"
    action: wait
    duration: 120000  # 2 minutes
    
  - step: "Attempt action after timeout"
    action: click
    selector: '[data-testid="appointments-link"]'
    
  - step: "Verify redirected to login"
    action: expectURL
    url: "/login"
    
  - step: "Verify session expired message"
    action: expect
    selector: '[data-testid="info-message"]'
    assertion: toContainText
    text: "Session expired"

checkpoints:
  - name: "Session Valid at 14min"
    verify: "Actions succeed before timeout"
  - name: "Session Invalid at 16min"
    verify: "Redirected to login after timeout"
```

---

### Edge Cases

#### TW-AUTH-005: Registration With Duplicate Email

**Requirement**: FR-001  
**Priority**: High

```yaml
test: "Registration Fails With Duplicate Email"
preconditions:
  - existing_user:
      email: "existing@example.com"
      
steps:
  - step: "Navigate to registration"
    action: goto
    url: "/register"
    
  - step: "Fill form with existing email"
    actions:
      - fill:
          selector: '[data-testid="name-input"]'
          value: "Another User"
      - fill:
          selector: '[data-testid="email-input"]'
          value: "existing@example.com"
      - fill:
          selector: '[data-testid="phone-input"]'
          value: "555-9999"
      - fill:
          selector: '[data-testid="password-input"]'
          value: "AnotherPass123!"
      - fill:
          selector: '[data-testid="confirm-password-input"]'
          value: "AnotherPass123!"
          
  - step: "Submit registration"
    action: click
    selector: '[data-testid="register-button"]'
    
  - step: "Verify error message"
    action: expect
    selector: '[data-testid="error-message"]'
    assertion: toContainText
    text: "Email already registered"
    
  - step: "Verify user not created"
    action: queryDatabase
    query: "SELECT COUNT(*) FROM users WHERE email='existing@example.com'"
    expected: 1

checkpoints:
  - name: "Duplicate Prevented"
    verify: "Only one user with email exists"
```

---

#### TW-AUTH-006: Admin Deactivates User - Access Revoked Immediately

**Requirement**: FR-049  
**Use Case**: UC-012  
**Priority**: High

```yaml
test: "Deactivated User Access Revoked Immediately"
preconditions:
  - existing_staff:
      email: "staff@clinic.com"
      status: "active"
      
steps:
  - step: "Staff user logs in"
    actions:
      - goto: "/login"
      - fill:
          selector: '[data-testid="email-input"]'
          value: "staff@clinic.com"
      - fill:
          selector: '[data-testid="password-input"]'
          value: "StaffPass123!"
      - click: '[data-testid="login-button"]'
          
  - step: "Verify staff dashboard accessible"
    action: expect
    selector: '[data-testid="staff-dashboard"]'
    assertion: toBeVisible
    
  - step: "In separate session, admin deactivates user"
    context: admin_session
    actions:
      - goto: "/admin/users"
      - fill:
          selector: '[data-testid="user-search"]'
          value: "staff@clinic.com"
      - click: '[data-testid="deactivate-user-button"]'
      - click: '[data-testid="confirm-deactivate"]'
          
  - step: "Staff attempts to navigate (original session)"
    action: click
    selector: '[data-testid="queue-link"]'
    
  - step: "Verify access denied"
    action: expectURL
    url: "/login"
    
  - step: "Verify error message"
    action: expect
    selector: '[data-testid="error-message"]'
    assertion: toContainText
    text: "Account deactivated"

checkpoints:
  - name: "Immediate Revocation"
    verify: "Active session invalidated"
  - name: "Audit Logged"
    verify: "Deactivation logged with admin ID"
```

---

### Error Scenarios

#### TW-AUTH-007: Registration With Weak Password

**Requirement**: FR-001, FR-005  
**Priority**: Medium

```yaml
test: "Registration Rejected - Weak Password"
steps:
  - step: "Navigate to registration"
    action: goto
    url: "/register"
    
  - step: "Fill form with weak password"
    actions:
      - fill:
          selector: '[data-testid="name-input"]'
          value: "Test User"
      - fill:
          selector: '[data-testid="email-input"]'
          value: "test@example.com"
      - fill:
          selector: '[data-testid="phone-input"]'
          value: "555-1234"
      - fill:
          selector: '[data-testid="password-input"]'
          value: "weak"
      - fill:
          selector: '[data-testid="confirm-password-input"]'
          value: "weak"
          
  - step: "Submit registration"
    action: click
    selector: '[data-testid="register-button"]'
    
  - step: "Verify validation error"
    action: expect
    selector: '[data-testid="password-error"]'
    assertion: toContainText
    text: "Password must be at least 10 characters"

checkpoints:
  - name: "Password Strength Enforced"
    verify: "User not created with weak password"
```

---

#### TW-AUTH-008: Login With Invalid Credentials

**Requirement**: FR-005  
**Priority**: High

```yaml
test: "Login Fails With Wrong Password"
preconditions:
  - existing_user:
      email: "user@example.com"
      password_hash: "$2b$12$validhash..."
      
steps:
  - step: "Navigate to login"
    action: goto
    url: "/login"
    
  - step: "Enter valid email, wrong password"
    actions:
      - fill:
          selector: '[data-testid="email-input"]'
          value: "user@example.com"
      - fill:
          selector: '[data-testid="password-input"]'
          value: "WrongPassword123!"
          
  - step: "Submit login"
    action: click
    selector: '[data-testid="login-button"]'
    
  - step: "Verify error message"
    action: expect
    selector: '[data-testid="error-message"]'
    assertion: toContainText
    text: "Invalid credentials"
    
  - step: "Verify not redirected"
    action: expectURL
    url: "/login"
    
  - step: "Verify no JWT token issued"
    action: expectCookie
    name: "auth_token"
    exists: false

checkpoints:
  - name: "Login Rejected"
    verify: "No session created"
  - name: "Generic Error"
    verify: "Error message doesn't reveal user existence"
```

---

#### TW-AUTH-009: Non-Admin Attempts User Creation

**Requirement**: FR-002, FR-003  
**Priority**: High

```yaml
test: "Patient Cannot Create User Accounts"
preconditions:
  - authenticated_as: "Patient"
  
steps:
  - step: "Attempt direct API call to create user"
    action: apiRequest
    method: POST
    url: "/api/admin/users"
    body:
      name: "Hacker User"
      email: "hacker@evil.com"
      role: "Admin"
    
  - step: "Verify 403 Forbidden response"
    action: expectResponse
    status: 403
    
  - step: "Verify user not created"
    action: queryDatabase
    query: "SELECT COUNT(*) FROM users WHERE email='hacker@evil.com'"
    expected: 0

checkpoints:
  - name: "Authorization Enforced"
    verify: "Non-admin blocked from user creation"
  - name: "Audit Log"
    verify: "Unauthorized attempt logged"
```

---

## Test Data

### Valid User Credentials

```yaml
test_users:
  - role: Patient
    email: "patient@example.com"
    password: "PatientPass123!"
    name: "Test Patient"
    
  - role: Staff
    email: "staff@clinic.com"
    password: "StaffPass123!"
    name: "Test Staff"
    
  - role: Admin
    email: "admin@clinic.com"
    password: "AdminPass123!"
    name: "Test Admin"
```

### Invalid Credentials

```yaml
invalid_passwords:
  - "weak"              # Too short
  - "12345678"          # No letters
  - "password"          # Too common
  - "   "               # Whitespace only
  - ""                  # Empty

invalid_emails:
  - "notanemail"        # Invalid format
  - "@example.com"      # Missing local part
  - "user@"             # Missing domain
```

---

## Traceability Matrix

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

---

## Execution Notes

- Run authentication tests in isolation to avoid session conflicts
- Clear cookies and local storage between tests
- Use separate database instances for concurrent test runs
- Mock email service for activation email verification
- Implement wait strategies for session timeout tests (or use clock mocking)

---

**Generated**: 2026-06-10  
**Source**: MasterTestPlan.md  
**Framework**: Playwright + TypeScript
