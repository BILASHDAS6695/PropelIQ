# Task 001: Structured Session Payload and Store Contract

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-015 |
| **Epic** | EP-001 |
| **Layer** | Application + Infrastructure |
| **Priority** | Critical |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | US-014 login flow |

## Objective

Move session storage from a plain string value to a structured payload that contains only non-sensitive identifiers:

- `userId`
- `role`
- `loginTimestamp`
- `lastActivityTimestamp`
- `sessionId` (recommended, to align with JWT `sid` claim)

## Acceptance Criteria Covered

- AC: Session includes userId, role, loginTimestamp, lastActivityTimestamp
- AC: No sensitive data (PHI) stored in session

## Files to Modify

| File | Change |
|------|--------|
| `src/HealthPlatform.Application/Interfaces/ISessionStore.cs` | Update contract to store/retrieve a structured session object |
| `src/HealthPlatform.Application/Features/Auth/` | Add `SessionState` record/model |
| `src/HealthPlatform.Infrastructure/Cache/RedisSessionStore.cs` | Serialize/deserialize JSON payload and preserve 15-minute TTL |
| `src/HealthPlatform.Application/Features/Auth/LoginCommandHandler.cs` | Store structured session on login |
| `src/HealthPlatform.Application/Features/Auth/RefreshTokenCommandHandler.cs` | Store structured session on refresh |

---

## Implementation Steps

### 1. Add a session state model

Create an immutable model in Application layer, for example:

```csharp
public sealed record SessionState(
    Guid UserId,
    string Role,
    DateTimeOffset LoginTimestamp,
    DateTimeOffset LastActivityTimestamp,
    Guid SessionId);
```

### 2. Update `ISessionStore` contract

Replace string-based session value methods with typed methods:

- `SetSessionAsync(SessionState session, CancellationToken ct = default)`
- `GetSessionAsync(Guid userId, CancellationToken ct = default)`
- `DeleteSessionAsync(Guid userId, CancellationToken ct = default)`
- `RefreshActivityAsync(Guid userId, DateTimeOffset activityAt, CancellationToken ct = default)`

Keep key format `session:{userId}` and 15-minute sliding TTL.

### 3. Update Redis implementation

- Serialize `SessionState` to JSON when setting session
- Deserialize from Redis when reading session
- On activity refresh:
  - update `LastActivityTimestamp`
  - reset TTL to 15 minutes

### 4. Wire login/refresh handlers

When creating a session in login and token refresh handlers:

- set `LoginTimestamp = UtcNow` for login
- set `LastActivityTimestamp = UtcNow`
- set `Role` from user role
- set `SessionId` to the generated `sessionId`

---

## Design Notes

- Session payload must not include PHI, names, phone, or clinical data.
- Keep the payload minimal so Redis memory usage stays predictable.
- Include `SessionId` to support JWT `sid` consistency checks in middleware.

## Acceptance Checklist

- [ ] Session state model created in Application layer
- [ ] `ISessionStore` updated to typed contract
- [ ] Redis store reads/writes JSON session payload
- [ ] Login and refresh flows write structured session state
- [ ] No PHI fields present in session payload
