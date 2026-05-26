# Task 004: Frontend Session Expiry Handling and UX Redirect

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-015 |
| **Epic** | EP-001 |
| **Layer** | Frontend (Angular) |
| **Priority** | High |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | Task 002 |

## Objective

Detect backend `401 Session expired` responses and provide a clear user experience:

- clear local auth state
- redirect user to login
- display session-expired message

## Acceptance Criteria Covered

- AC: Frontend detects 401 session expired -> redirects to login with "Session expired" message
- AC: Concurrent tabs share session behavior (indirectly validated via shared storage state)

## Files to Modify

| File | Change |
|------|--------|
| `src/health-platform-ui/src/app/core/interceptors/error.interceptor.ts` | Branch 401 handling for session expiry details |
| `src/health-platform-ui/src/app/core/auth/auth.service.ts` | Add helper to clear auth state only |
| `src/health-platform-ui/src/app/features/auth/login/login.component.ts` | Read query param and show "Session expired" banner |

---

## Implementation Steps

### 1. Update error interceptor 401 flow

- Inspect `error.error?.detail`
- If detail equals `Session expired`:
  - clear auth storage
  - navigate to `/login?expired=true`
- Keep existing generic 401 behavior as fallback

### 2. Centralize local auth clear

In `AuthService`, add a helper method (e.g., `clearAuthState()`):

- clear signal values
- remove `auth_token`, `auth_user`, `auth_userId`, `refresh_token`

Use this helper from logout and interceptor-driven flows.

### 3. Show session-expired message on login page

In `LoginComponent`:

- read `expired` query parameter
- show a warning/info message like `Session expired. Please sign in again.`

---

## Design Notes

- Keep message wording generic and non-sensitive.
- Use query param to support direct deep-link return to login page.
- Ensure banner state does not override existing server error display.

## Acceptance Checklist

- [ ] Error interceptor differentiates `Session expired` from other 401 responses
- [ ] Redirect to `/login?expired=true` works
- [ ] Login page shows session-expired banner
- [ ] Auth storage is consistently cleared in one place
