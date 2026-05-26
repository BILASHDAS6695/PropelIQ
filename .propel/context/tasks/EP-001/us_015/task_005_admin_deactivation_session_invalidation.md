# Task 005: Admin Deactivation Session Invalidation

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-015 |
| **Epic** | EP-001 |
| **Layer** | Application + API |
| **Priority** | High |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | Task 001 |

## Objective

When an admin deactivates a user account, immediately invalidate active session and refresh token so access is cut off without waiting for timeout.

## Acceptance Criteria Covered

- AC: Admin deactivating a user immediately deletes their Redis session

## Files to Create/Modify

| File | Change |
|------|--------|
| `src/HealthPlatform.Application/Features/Admin/` | Add deactivation command/handler (if not already present) |
| `src/HealthPlatform.Application/Interfaces/` | Reuse `ISessionStore` + `IJwtTokenService` in handler |
| `src/HealthPlatform.Api/Controllers/` | Ensure admin endpoint invokes deactivation handler |

---

## Implementation Steps

### 1. Locate admin deactivation workflow

If deactivation endpoint/handler already exists:

- extend existing handler

If not:

- create `DeactivateUserCommand` + handler in Admin feature
- require admin authorization at controller level

### 2. Invalidate auth artifacts on deactivation

After setting `user.IsActive = false` and persisting:

- call `ISessionStore.DeleteSessionAsync(userId)`
- call `IJwtTokenService.RevokeRefreshTokenAsync(userId)`

### 3. Response behavior

- Return success for deactivation even if session key/token key is already absent
- Keep operation idempotent

---

## Design Notes

- Invalidation should happen in the same request path as deactivation.
- Reusing existing auth abstractions avoids Redis coupling in Application handlers.
- Consider emitting audit event `UserDeactivated` with actor/admin id.

## Acceptance Checklist

- [ ] Deactivation flow exists and is admin-protected
- [ ] Deactivation revokes Redis session immediately
- [ ] Deactivation revokes refresh token immediately
- [ ] Behavior is idempotent when session/token keys are absent
