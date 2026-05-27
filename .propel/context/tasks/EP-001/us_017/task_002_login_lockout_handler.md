# Task 002: Enforce Account Lockout & Password Expiry in LoginCommandHandler

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-017 |
| **Epic** | EP-001 |
| **Layer** | Application — CQRS Handler |
| **Priority** | High |
| **Estimated Effort** | 1.5 hours |
| **Dependencies** | Task 001 — entity fields, `AccountSecuritySettings`, and migration must exist |

## Objective

Update `LoginCommandHandler` to enforce account lockout (gate before bcrypt, increment counter on failure, set `LockoutEnd` at threshold) and password expiry (return `PasswordChangeRequired: true` when `CredentialExpiresAt` has passed). Extend `LoginResult` with the two new fields required by Task 003.

---

## Implementation Steps

### 1. `src/HealthPlatform.Application/Features/Auth/LoginCommand.cs` — Extend `LoginResult`

Add two optional trailing properties with defaults so all existing call sites remain valid:

```csharp
public sealed record LoginResult(
    bool    IsSuccess,
    string? AccessToken,
    string? RefreshToken,
    int     ExpiresIn,
    string? Error,
    bool    PasswordChangeRequired  = false,
    int?    LockoutSecondsRemaining = null);
```

### 2. `src/HealthPlatform.Application/Features/Auth/LoginCommandHandler.cs` — Full lockout lifecycle

**Constructor changes** — inject `IOptions<AccountSecuritySettings>`:

```csharp
private readonly AccountSecuritySettings _security;

public LoginCommandHandler(
    IUnitOfWork uow,
    IPasswordHasher hasher,
    IJwtTokenService jwt,
    ISessionStore session,
    IOptions<AccountSecuritySettings> security,
    ILogger<LoginCommandHandler> logger)
{
    _security = security.Value;
    // ... existing assignments
}
```

**Handler body changes** (in `Handle`):

1. Capture `var now = DateTimeOffset.UtcNow;` **before** the inactive check (Step 2).

2. **Step 3 — Lockout gate** (insert before password check):
```csharp
// ── 3. Lockout gate ───────────────────────────────────────────────────
if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > now)
{
    var remaining = (int)Math.Ceiling(
        (user.LockoutEnd.Value - now).TotalSeconds);

    await WriteAuditAsync(
        user.Id, "LoginBlockedByLockout", nameof(User), user.Id,
        new { lockoutEndsAt = user.LockoutEnd.Value, remainingSeconds = remaining },
        cancellationToken);

    return LockedOut(remaining);
}
```

3. **Step 4 — Password failure branch** — replace plain `Fail` with counter+lockout logic:
```csharp
if (!_hasher.Verify(request.Password, user.PasswordHash))
{
    user.FailedLoginAttempts++;

    if (user.FailedLoginAttempts >= _security.MaxFailedLoginAttempts)
    {
        user.LockoutEnd = now.AddMinutes(_security.LockoutDurationMinutes);

        await WriteAuditAsync(
            user.Id, "AccountLocked", nameof(User), user.Id,
            new { failedAttempts = user.FailedLoginAttempts, lockoutEnd = user.LockoutEnd.Value },
            cancellationToken);

        _logger.LogWarning(
            "Account {UserId} locked after {Attempts} failed attempts.",
            user.Id, user.FailedLoginAttempts);
    }
    else
    {
        await WriteAuditAsync(
            user.Id, "LoginFailed", nameof(User), user.Id,
            new { reason = "invalid_password", failedAttempts = user.FailedLoginAttempts },
            cancellationToken);

        _logger.LogWarning(
            "Login failed: invalid password for user {UserId}. Attempt {Attempt}/{Max}.",
            user.Id, user.FailedLoginAttempts, _security.MaxFailedLoginAttempts);
    }

    await _uow.SaveChangesAsync(cancellationToken);
    return Fail("Invalid email or password.");
}
```

4. **Step 5 — Successful auth** — reset counter after password verified:
```csharp
user.FailedLoginAttempts = 0;
user.LockoutEnd          = null;
```

5. **Step 11 — Password expiry check** (after audit, before return):
```csharp
var passwordChangeRequired =
    user.CredentialExpiresAt.HasValue &&
    user.CredentialExpiresAt.Value <= now;

return new LoginResult(
    IsSuccess:              true,
    AccessToken:            tokenPair.AccessToken,
    RefreshToken:           tokenPair.RefreshToken,
    ExpiresIn:              tokenPair.ExpiresIn,
    Error:                  null,
    PasswordChangeRequired: passwordChangeRequired);
```

6. **Add `LockedOut` static helper**:
```csharp
private static LoginResult LockedOut(int secondsRemaining)
    => new(false, null, null, 0,
        $"Account is locked. Try again in {secondsRemaining} seconds.",
        LockoutSecondsRemaining: secondsRemaining);
```

---

## Audit Events Added

| Event | Trigger |
|-------|---------|
| `LoginBlockedByLockout` | Login attempted while lockout is active |
| `AccountLocked` | Failure count reaches `MaxFailedLoginAttempts` |
| `LoginFailed` | Each failed password attempt (with attempt count) |

---

## Affected Files

| File | Change |
|------|--------|
| `src/HealthPlatform.Application/Features/Auth/LoginCommand.cs` | +2 properties on `LoginResult` |
| `src/HealthPlatform.Application/Features/Auth/LoginCommandHandler.cs` | Lockout gate, counter logic, expiry check |

---

## Acceptance Criteria

- [ ] `LoginResult` has `PasswordChangeRequired` and `LockoutSecondsRemaining` with correct defaults
- [ ] Lockout gate runs **before** `_hasher.Verify` (prevents timing-attack enumeration)
- [ ] `FailedLoginAttempts` incremented and saved on each wrong password
- [ ] `LockoutEnd` set to `now + LockoutDurationMinutes` at threshold; `AccountLocked` audit written
- [ ] `FailedLoginAttempts` and `LockoutEnd` reset to `0` / `null` on success
- [ ] `PasswordChangeRequired` is `true` when `CredentialExpiresAt <= now`
- [ ] `dotnet build` passes (0 errors)

## Verification

```powershell
cd src
dotnet build HealthPlatform.sln --no-restore
```
