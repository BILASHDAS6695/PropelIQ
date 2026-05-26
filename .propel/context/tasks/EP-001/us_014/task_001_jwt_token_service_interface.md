# Task 001: IJwtTokenService Interface and Token Result Records

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-014 |
| **Epic** | EP-001 |
| **Layer** | Application (interface + records) |
| **Priority** | Critical |
| **Estimated Effort** | 20 minutes |
| **Dependencies** | None |

## Objective

Define the Application-layer contract for JWT token generation and refresh-token
lifecycle management. The interface keeps the Application layer independent of
any JWT library — only the Infrastructure `JwtTokenService` (Task 004) knows
about `System.IdentityModel.Tokens.Jwt`.

Two files are created:

1. **`TokenResult.cs`** — immutable record returned by every successful auth operation.
2. **`IJwtTokenService.cs`** — interface for token generation, refresh-token
   storage, validation, and revocation.

## Acceptance Criteria Covered

- AC: Login endpoint returns accessToken (JWT, 30-min), refreshToken (7-day), expiresIn
- AC: JWT contains claims: userId, email, role, sessionId
- AC: Refresh tokens are single-use (rotated on each refresh)

## Files to Create

| File | Layer |
|------|-------|
| `src/HealthPlatform.Application/Features/Auth/TokenResult.cs` | Application |
| `src/HealthPlatform.Application/Interfaces/IJwtTokenService.cs` | Application |

---

## Implementation Steps

### 1. Create `TokenResult` Record

**File:** `src/HealthPlatform.Application/Features/Auth/TokenResult.cs`

```csharp
namespace HealthPlatform.Application.Features.Auth;

/// <summary>
/// Holds the token pair issued after a successful login or refresh.
/// </summary>
/// <param name="AccessToken">Signed JWT valid for 30 minutes.</param>
/// <param name="RefreshToken">Opaque random token valid for 7 days (single-use).</param>
/// <param name="ExpiresIn">Access-token lifetime in seconds (1800).</param>
/// <param name="SessionId">Unique identifier for this login session, embedded in the JWT.</param>
public sealed record TokenResult(
    string AccessToken,
    string RefreshToken,
    int    ExpiresIn,
    Guid   SessionId);
```

### 2. Create `IJwtTokenService` Interface

**File:** `src/HealthPlatform.Application/Interfaces/IJwtTokenService.cs`

```csharp
using HealthPlatform.Application.Features.Auth;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Generates JWT access tokens and manages single-use refresh tokens
/// stored in the distributed cache.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Builds a signed JWT access token and a cryptographically random
    /// refresh token for the given user and session.
    /// </summary>
    /// <param name="user">The authenticated user (email, role, and ID are embedded as claims).</param>
    /// <param name="sessionId">Unique session identifier embedded in the JWT <c>sid</c> claim.</param>
    /// <returns>A <see cref="TokenResult"/> containing both tokens and the expiry in seconds.</returns>
    TokenResult GenerateTokenPair(User user, Guid sessionId);

    /// <summary>
    /// Persists a refresh token in the distributed cache under the key
    /// <c>refresh:{userId}</c> with a 7-day TTL, overwriting any existing entry.
    /// </summary>
    Task StoreRefreshTokenAsync(
        Guid userId,
        string refreshToken,
        CancellationToken ct = default);

    /// <summary>
    /// Validates that the provided <paramref name="refreshToken"/> matches the
    /// cached value for <paramref name="userId"/>. If valid, <strong>atomically
    /// deletes the entry</strong> (single-use enforcement) and returns
    /// <c>true</c>. Returns <c>false</c> on mismatch or cache miss.
    /// </summary>
    Task<bool> ValidateAndConsumeRefreshTokenAsync(
        Guid   userId,
        string refreshToken,
        CancellationToken ct = default);

    /// <summary>
    /// Removes the refresh token for the given user (logout / account lockout).
    /// No-ops if the key does not exist.
    /// </summary>
    Task RevokeRefreshTokenAsync(Guid userId, CancellationToken ct = default);
}
```

---

## Design Notes

- `GenerateTokenPair` is **synchronous** — JWT signing and random token generation
  are CPU-bound operations; no I/O is required.
- Refresh token storage and validation are **async** — they involve Redis I/O.
- `ValidateAndConsumeRefreshTokenAsync` performs both validation *and* deletion
  in a single method to prevent TOCTOU races (the caller cannot forget to delete
  after validating).
- The `sessionId` is separate from `userId` and is rotated on every refresh, so
  revoked sessions cannot be reused even with a valid refresh token.

## Acceptance Checklist

- [ ] `TokenResult` record created in `Features/Auth/`
- [ ] `IJwtTokenService` interface created in `Interfaces/`
- [ ] All four interface members defined with XML doc comments
- [ ] Solution builds with 0 errors
