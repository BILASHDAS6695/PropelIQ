# Task 002: LoginCommand, LoginCommandValidator, and LoginCommandHandler

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-014 |
| **Epic** | EP-001 |
| **Layer** | Application / CQRS |
| **Priority** | Critical |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | Task 001 (IJwtTokenService, TokenResult) |

## Objective

Implement the full MediatR login flow:
1. **`LoginCommand`** + **`LoginResult`** — request/response records.
2. **`LoginCommandValidator`** — FluentValidation input guards.
3. **`LoginCommandHandler`** — orchestrates credential verification, session
   creation, Redis session + refresh-token storage, `LastLoginAt` update,
   and audit logging.

## Acceptance Criteria Covered

- AC: Login endpoint accepts email + password, returns accessToken, refreshToken, expiresIn
- AC: JWT contains claims: userId, email, role, sessionId
- AC: Invalid credentials return 401 with generic message (no email/password hint)
- AC: Login creates a Redis session entry with 15-minute TTL
- AC: Successful login logged in audit trail
- AC: Failed login logged in audit trail (without password)
- AC: Locked account attempts login → 401 "Account temporarily locked"
- AC: Deactivated account → 401 "Account is inactive"

## Files to Create

| File | Purpose |
|------|---------|
| `src/HealthPlatform.Application/Features/Auth/LoginCommand.cs` | Request + result records |
| `src/HealthPlatform.Application/Features/Auth/LoginCommandValidator.cs` | FluentValidation rules |
| `src/HealthPlatform.Application/Features/Auth/LoginCommandHandler.cs` | MediatR handler |

---

## Implementation Steps

### 1. Create `LoginCommand.cs`

**File:** `src/HealthPlatform.Application/Features/Auth/LoginCommand.cs`

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Auth;

public sealed record LoginCommand(
    string Email,
    string Password) : IRequest<LoginResult>;

public sealed record LoginResult(
    bool    IsSuccess,
    string? AccessToken,
    string? RefreshToken,
    int     ExpiresIn,
    string? Error);
```

### 2. Create `LoginCommandValidator.cs`

**File:** `src/HealthPlatform.Application/Features/Auth/LoginCommandValidator.cs`

```csharp
using FluentValidation;

namespace HealthPlatform.Application.Features.Auth;

internal sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty();
    }
}
```

> **Note:** Login validation is intentionally minimal (not-empty + email format).
> Full password complexity rules are applied only at registration (US-013).
> This prevents information leakage about which field failed.

### 3. Create `LoginCommandHandler.cs`

**File:** `src/HealthPlatform.Application/Features/Auth/LoginCommandHandler.cs`

```csharp
using System.Text.Json;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Application.Features.Auth;

internal sealed class LoginCommandHandler
    : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IUnitOfWork        _uow;
    private readonly IPasswordHasher    _hasher;
    private readonly IJwtTokenService   _jwt;
    private readonly ISessionStore      _session;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IUnitOfWork uow,
        IPasswordHasher hasher,
        IJwtTokenService jwt,
        ISessionStore session,
        ILogger<LoginCommandHandler> logger)
    {
        _uow     = uow;
        _hasher  = hasher;
        _jwt     = jwt;
        _session = session;
        _logger  = logger;
    }

    public async Task<LoginResult> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        var userRepo = _uow.Repository<User>();
        var spec     = new UserByEmailSpecification(request.Email);
        var matches  = await userRepo.GetAsync(spec, cancellationToken);
        var user     = matches.FirstOrDefault();

        // ── 1. User not found — return generic message ────────────────────
        if (user is null)
        {
            await WriteAuditAsync(Guid.Empty, "LoginFailed",
                "User", Guid.Empty, new { reason = "user_not_found" }, cancellationToken);
            _logger.LogWarning("Login failed: email not found.");
            return Fail("Invalid email or password.");
        }

        // ── 2. Account inactive ───────────────────────────────────────────
        if (!user.IsActive)
        {
            await WriteAuditAsync(user.Id, "LoginFailed",
                nameof(User), user.Id, new { reason = "account_inactive" }, cancellationToken);
            return Fail("Account is inactive.");
        }

        // ── 3. Password verification ──────────────────────────────────────
        if (!_hasher.Verify(request.Password, user.PasswordHash))
        {
            await WriteAuditAsync(user.Id, "LoginFailed",
                nameof(User), user.Id, new { reason = "invalid_password" }, cancellationToken);
            _logger.LogWarning("Login failed: invalid password for user {UserId}.", user.Id);
            return Fail("Invalid email or password.");
        }

        // ── 4. Generate session + token pair ──────────────────────────────
        var sessionId  = Guid.NewGuid();
        var tokenPair  = _jwt.GenerateTokenPair(user, sessionId);

        // ── 5. Store Redis session (15-min sliding TTL) ───────────────────
        await _session.SetSessionAsync(
            user.Id.ToString(), sessionId.ToString(), cancellationToken);

        // ── 6. Store refresh token in Redis (7-day TTL) ───────────────────
        await _jwt.StoreRefreshTokenAsync(
            user.Id, tokenPair.RefreshToken, cancellationToken);

        // ── 7. Update LastLoginAt ─────────────────────────────────────────
        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _uow.SaveChangesAsync(cancellationToken);

        // ── 8. Audit: successful login ────────────────────────────────────
        await WriteAuditAsync(user.Id, "LoginSucceeded",
            nameof(User), user.Id,
            new { sessionId = sessionId.ToString() }, cancellationToken);

        return new LoginResult(true,
            tokenPair.AccessToken,
            tokenPair.RefreshToken,
            tokenPair.ExpiresIn,
            null);
    }

    // ── helpers ───────────────────────────────────────────────────────────────
    private static LoginResult Fail(string error)
        => new(false, null, null, 0, error);

    private async Task WriteAuditAsync(
        Guid userId, string action, string entityType,
        Guid entityId, object details, CancellationToken ct)
    {
        var auditRepo = _uow.Repository<AuditLog>();
        auditRepo.Add(new AuditLog
        {
            Id          = Guid.NewGuid(),
            UserId      = userId == Guid.Empty ? Guid.Empty : userId,
            Action      = action,
            EntityType  = entityType,
            EntityId    = entityId,
            Timestamp   = DateTimeOffset.UtcNow,
            Details     = System.Text.Json.JsonDocument.Parse(
                              JsonSerializer.Serialize(details)),
            PreviousHash = null,
            CurrentHash  = string.Empty   // interceptor fills the chain on Save
        });
        await _uow.SaveChangesAsync(ct);
    }
}
```

---

## Design Notes

### Generic Error Messages
Both "user not found" and "invalid password" return the same `"Invalid email or
password."` message to prevent **user enumeration** (OWASP A01).

### Failed Login Audit for Unknown Email
When no matching user is found, `UserId = Guid.Empty` is recorded. This violates
the `audit_logs.user_id` FK constraint. To avoid this, the audit entry for
unknown-email failures should be **skipped** or logged only to structured logs
(not the AuditLog table). Update `WriteAuditAsync` to guard:

```csharp
private async Task WriteAuditAsync(
    Guid userId, string action, string entityType,
    Guid entityId, object details, CancellationToken ct)
{
    // Skip DB audit for anonymous failures — no valid userId to FK-link.
    if (userId == Guid.Empty)
    {
        _logger.LogInformation("Auth audit (anonymous): {Action} on {EntityType}",
            action, entityType);
        return;
    }

    var auditRepo = _uow.Repository<AuditLog>();
    auditRepo.Add(new AuditLog
    {
        Id           = Guid.NewGuid(),
        UserId       = userId,
        Action       = action,
        EntityType   = entityType,
        EntityId     = entityId,
        Timestamp    = DateTimeOffset.UtcNow,
        Details      = JsonDocument.Parse(JsonSerializer.Serialize(details)),
        PreviousHash = null,
        CurrentHash  = string.Empty
    });
    await _uow.SaveChangesAsync(ct);
}
```

### No `ICurrentUserService` in Handler
The login handler runs **before** authentication completes — `ICurrentUserService`
would return `Guid.Empty`. Audit entries are written directly with the resolved
`user.Id`.

### `LastLoginAt` Save
`user.LastLoginAt = DateTimeOffset.UtcNow` marks the entity as Modified; the
`AuditSaveChangesInterceptor` will also create an automatic `Updated` audit entry
for the User entity. This is acceptable — both the explicit auth audit and the
automatic interceptor audit are written.

---

## Acceptance Checklist

- [ ] `LoginCommand` and `LoginResult` records created
- [ ] `LoginCommandValidator` created with email + password NotEmpty rules
- [ ] `LoginCommandHandler` resolves user by email (case-insensitive via `UserByEmailSpecification`)
- [ ] Returns generic `"Invalid email or password."` for both user-not-found and wrong-password
- [ ] Returns `"Account is inactive."` for deactivated accounts
- [ ] Creates Redis session entry after successful login
- [ ] Stores refresh token via `IJwtTokenService.StoreRefreshTokenAsync`
- [ ] Writes `LoginSucceeded` audit log on success
- [ ] Skips DB audit for unknown-email (no FK violation)
- [ ] Solution builds with 0 errors
