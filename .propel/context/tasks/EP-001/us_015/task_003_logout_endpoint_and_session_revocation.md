# Task 003: Logout Endpoint and Immediate Session Revocation

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-015 |
| **Epic** | EP-001 |
| **Layer** | Application + API + Frontend |
| **Priority** | High |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | Tasks 001-002 |

## Objective

Implement explicit logout so sessions are revoked immediately server-side and client tokens are cleared.

## Acceptance Criteria Covered

- AC: Explicit logout deletes Redis session immediately

## Files to Create/Modify

| File | Change |
|------|--------|
| `src/HealthPlatform.Application/Features/Auth/LogoutCommand.cs` | Create command/result |
| `src/HealthPlatform.Application/Features/Auth/LogoutCommandHandler.cs` | Delete session + refresh token |
| `src/HealthPlatform.Api/Controllers/AuthController.cs` | Add `POST /api/auth/logout` endpoint |
| `src/health-platform-ui/src/app/core/auth/auth.service.ts` | Add API-backed `logout()` method |

---

## Implementation Steps

### 1. Add logout command and handler

`LogoutCommand` should carry current user id from controller context.

Handler flow:

- call `ISessionStore.DeleteSessionAsync(userId)`
- call `IJwtTokenService.RevokeRefreshTokenAsync(userId)`
- optionally write audit entry `LogoutSucceeded`
- return success result

### 2. Add API endpoint

In `AuthController`:

- `POST /api/auth/logout`
- requires `[Authorize]`
- resolves `userId` from claims (`sub`/`NameIdentifier`)
- dispatches `LogoutCommand`
- returns `204 NoContent`

### 3. Update frontend logout

In `AuthService.logout()`:

- call backend `/api/auth/logout` with bearer token
- regardless of response, clear `sessionStorage` and `localStorage`
- redirect to `/login`

---

## Design Notes

- Logout should be idempotent: deleting missing keys should still return success.
- Keep client-side token clear in `finalize` to avoid stale auth state.

## Acceptance Checklist

- [ ] `POST /api/auth/logout` endpoint exists and is authorized
- [ ] Logout command handler deletes Redis session and refresh token
- [ ] Frontend calls logout API and clears client tokens
- [ ] Logout works even if session key already expired
