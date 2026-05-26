# Task 002: API Session Validation Middleware with Sliding Timeout

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-015 |
| **Epic** | EP-001 |
| **Layer** | API Middleware |
| **Priority** | Critical |
| **Estimated Effort** | 60 minutes |
| **Dependencies** | Task 001 |

## Objective

Enforce server-side session validity for authenticated API requests:

1. Validate session exists in Redis for authenticated user
2. Ensure session matches JWT context (at minimum `userId`; ideally `sid`)
3. Refresh session activity timestamp and TTL on every valid request
4. Return HTTP 401 with `Session expired` when session is missing/expired

## Acceptance Criteria Covered

- AC: Each API request validates session exists in Redis (`session:{userId}`)
- AC: Valid session TTL refreshed to 15 minutes on each request (sliding expiration)
- AC: Expired session (Redis key absent) -> API returns 401 "Session expired"

## Files to Create/Modify

| File | Change |
|------|--------|
| `src/HealthPlatform.Api/Middleware/SessionValidationMiddleware.cs` | Create middleware |
| `src/HealthPlatform.Api/Program.cs` | Register middleware after `UseAuthentication()` and before `UseAuthorization()` |
| `src/HealthPlatform.Application/Interfaces/ICurrentUserService.cs` (optional) | Expose session id claim if needed |

---

## Implementation Steps

### 1. Create `SessionValidationMiddleware`

Flow:

- If endpoint allows anonymous (`IAllowAnonymous`) -> skip
- If user is unauthenticated -> skip (authentication middleware handles challenge)
- Read `sub` claim as `userId`
- Read `sid` claim as `sessionId` (if available)
- Load session from `ISessionStore`
- If session is missing -> return `401` with `ProblemDetails` detail `Session expired`
- If `sid` mismatch -> return `401` with `Session expired`
- If valid -> call `RefreshActivityAsync(userId, UtcNow)` and continue

### 2. Register middleware in pipeline

In `Program.cs`:

- keep `UseAuthentication()`
- add `UseMiddleware<SessionValidationMiddleware>()`
- then `UseAuthorization()`

This guarantees claims are available before session validation.

### 3. Exclusions

Ensure middleware skips these paths:

- `/api/auth/login`
- `/api/auth/register`
- `/api/auth/refresh`
- `/health`
- `/swagger` and `/swagger/*`
- `/hubs/*` (if hub auth should be handled separately)

---

## Design Notes

- Returning `Session expired` should not reveal internal Redis/cache details.
- Treat any cache error as fail-safe unauthorized for protected endpoints.
- Structured logs should include `userId`, `path`, and `correlationId` but no PHI.

## Acceptance Checklist

- [ ] Middleware validates session presence for protected API requests
- [ ] Session TTL is refreshed on each valid request
- [ ] Missing/expired session returns 401 with `Session expired`
- [ ] Anonymous/public routes are excluded correctly
- [ ] Middleware added in correct order in `Program.cs`
