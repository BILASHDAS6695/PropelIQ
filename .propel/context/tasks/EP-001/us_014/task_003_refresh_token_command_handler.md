# Task 003: RefreshTokenCommand and RefreshTokenCommandHandler

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-014 |
| **Epic** | EP-001 |
| **Layer** | Application / CQRS |
| **Priority** | Critical |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | Task 001 (IJwtTokenService, TokenResult) |

## Objective

Implement the token-refresh CQRS pair that:

1. Accepts a `userId` + `refreshToken` from the client.
2. Validates the refresh token against Redis (**consuming it** — single-use).
3. Loads the user, verifies the account is still active.
4. Generates a new session ID + new token pair.
5. Updates the Redis session (15-min TTL) and stores the new refresh token (7-day).
6. Returns the new `accessToken`, `refreshToken`, and `expiresIn`.

## Acceptance Criteria Covered

- AC: Refresh token endpoint accepts valid refresh token, issues new pair
- AC: Refresh tokens are single-use (rotated on each refresh)
- AC: Expired refresh token → 401, must re-authenticate

## Files to Create

| File | Purpose |
|------|---------|
| `src/HealthPlatform.Application/Features/Auth/RefreshTokenCommand.cs` | Request + result records |
| `src/HealthPlatform.Application/Features/Auth/RefreshTokenCommandHandler.cs` | MediatR handler |

---

## Implementation Steps

### 1. Create `RefreshTokenCommand.cs`

**File:** `src/HealthPlatform.Application/Features/Auth/RefreshTokenCommand.cs`

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Auth;

public sealed record RefreshTokenCommand(
    Guid   UserId,
    string RefreshToken) : IRequest<RefreshTokenResult>;

public sealed record RefreshTokenResult(
    bool    IsSuccess,
    string? AccessToken,
    string? RefreshToken,
    int     ExpiresIn,
    string? Error);
```

### 2. Create `RefreshTokenCommandHandler.cs`

**File:** `src/HealthPlatform.Application/Features/Auth/RefreshTokenCommandHandler.cs`

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Application.Features.Auth;

internal sealed class RefreshTokenCommandHandler
    : IRequestHandler<RefreshTokenCommand, RefreshTokenResult>
{
    private readonly IUnitOfWork      _uow;
    private readonly IJwtTokenService _jwt;
    private readonly ISessionStore    _session;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IUnitOfWork uow,
        IJwtTokenService jwt,
        ISessionStore session,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _uow     = uow;
        _jwt     = jwt;
        _session = session;
        _logger  = logger;
    }

    public async Task<RefreshTokenResult> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        // ── 1. Validate & consume the refresh token (single-use) ──────────
        var valid = await _jwt.ValidateAndConsumeRefreshTokenAsync(
            request.UserId, request.RefreshToken, cancellationToken);

        if (!valid)
        {
            _logger.LogWarning(
                "Token refresh failed: invalid or expired refresh token for user {UserId}.",
                request.UserId);
            return Fail("Invalid or expired refresh token.");
        }

        // ── 2. Load user — verify account is still active ─────────────────
        var userRepo = _uow.Repository<User>();
        var user     = await userRepo.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            _logger.LogWarning(
                "Token refresh failed: user {UserId} not found or inactive.",
                request.UserId);
            return Fail("Account is unavailable.");
        }

        // ── 3. Generate new session ID + token pair ───────────────────────
        var newSessionId = Guid.NewGuid();
        var tokenPair    = _jwt.GenerateTokenPair(user, newSessionId);

        // ── 4. Update Redis session (resets the 15-min sliding TTL) ───────
        await _session.SetSessionAsync(
            user.Id.ToString(), newSessionId.ToString(), cancellationToken);

        // ── 5. Store new refresh token (7-day TTL, old already consumed) ──
        await _jwt.StoreRefreshTokenAsync(
            user.Id, tokenPair.RefreshToken, cancellationToken);

        return new RefreshTokenResult(
            true,
            tokenPair.AccessToken,
            tokenPair.RefreshToken,
            tokenPair.ExpiresIn,
            null);
    }

    private static RefreshTokenResult Fail(string error)
        => new(false, null, null, 0, error);
}
```

---

## Design Notes

### Why `UserId` in the Request?
The refresh endpoint receives the `userId` from the client. This avoids parsing
the expired JWT on the server (which would require disabling lifetime validation).
The `userId` alone does not grant access — the matching refresh token in Redis
must also be present.

### Single-Use Enforcement
`ValidateAndConsumeRefreshTokenAsync` deletes the Redis key atomically. Even if
the client sends the same refresh token twice, the second call finds no entry
and returns `false`.

### No Refresh-Success Audit Log
The refresh operation does not write to `AuditLog`. Session maintenance is
high-frequency and audit logs should capture security-significant events.
`LoginSucceeded` and `LoginFailed` are the relevant audit points.

### `IRepository<T>.GetByIdAsync`
The handler calls `userRepo.GetByIdAsync(request.UserId, ct)`. Confirm this
method exists on `IRepository<T>` (it was defined in `IRepository.cs`). If it
is not present, use:

```csharp
var matches = await userRepo.GetAsync(
    new UserByIdSpecification(request.UserId), cancellationToken);
var user = matches.FirstOrDefault();
```

And create `UserByIdSpecification` in the Auth feature folder:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using System.Linq.Expressions;

namespace HealthPlatform.Application.Features.Auth;

internal sealed class UserByIdSpecification : ISpecification<User>
{
    private readonly Guid _id;
    public UserByIdSpecification(Guid id) => _id = id;
    public Expression<Func<User, bool>> Criteria => u => u.Id == _id;
    public List<Expression<Func<User, object>>> Includes => [];
    public Expression<Func<User, object>>? OrderBy => null;
    public bool IsPagingEnabled => false;
    public int Take => 0;
    public int Skip => 0;
}
```

---

## Acceptance Checklist

- [ ] `RefreshTokenCommand` and `RefreshTokenResult` records created
- [ ] Handler validates refresh token via `ValidateAndConsumeRefreshTokenAsync`
- [ ] Returns generic `"Invalid or expired refresh token."` on failure (no detail leakage)
- [ ] Re-checks `user.IsActive` before issuing new tokens
- [ ] Generates new `sessionId` (rotation) on every successful refresh
- [ ] Updates Redis session and stores new refresh token
- [ ] Solution builds with 0 errors
