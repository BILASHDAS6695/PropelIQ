# Task 004: JwtTokenService Implementation and DI Registration

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-014 |
| **Epic** | EP-001 |
| **Layer** | Infrastructure / Security |
| **Priority** | Critical |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | Task 001 (IJwtTokenService, TokenResult) |

## Objective

Provide the Infrastructure implementation of `IJwtTokenService` that:

1. **Builds signed JWTs** using `System.IdentityModel.Tokens.Jwt` with claims
   `sub` (userId), `email`, `role`, `sid` (sessionId), signed with HMAC-SHA256
   from `JwtSettings:SecretKey`.
2. **Generates cryptographically secure refresh tokens** using
   `RandomNumberGenerator`.
3. **Persists / validates / revokes refresh tokens** in Redis via `ICacheService`
   using the key `refresh:{userId}` with a 7-day TTL.
4. **Registers** itself and updates `appsettings.Development.json` with a
   `JwtSettings:RefreshTokenExpiryDays` value.

## Acceptance Criteria Covered

- AC: Login endpoint returns accessToken (JWT, 30-min), refreshToken (7-day), expiresIn
- AC: JWT contains claims: userId, email, role, sessionId
- AC: JWT signed with HMAC-SHA256 secret (configurable via environment variable)
- AC: Refresh tokens are single-use (rotated on each refresh)

## Files to Create / Modify

| File | Action |
|------|--------|
| `src/HealthPlatform.Infrastructure/Security/JwtTokenService.cs` | Create |
| `src/HealthPlatform.Infrastructure/DependencyInjection.cs` | Modify — add registration |

---

## Implementation Steps

### 1. Create `JwtTokenService.cs`

**File:** `src/HealthPlatform.Infrastructure/Security/JwtTokenService.cs`

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HealthPlatform.Application.Features.Auth;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace HealthPlatform.Infrastructure.Security;

internal sealed class JwtTokenService : IJwtTokenService
{
    private const int AccessTokenMinutes     = 30;
    private const int RefreshTokenDays       = 7;
    private const int RefreshTokenByteLength = 32; // 256-bit → 44-char Base64

    private readonly string           _secretKey;
    private readonly string           _issuer;
    private readonly string           _audience;
    private readonly ICacheService    _cache;
    private readonly TimeSpan         _refreshTtl = TimeSpan.FromDays(RefreshTokenDays);

    public JwtTokenService(IConfiguration configuration, ICacheService cache)
    {
        var jwt    = configuration.GetSection("JwtSettings");
        _secretKey = jwt["SecretKey"]
            ?? throw new InvalidOperationException("JwtSettings:SecretKey is required.");
        _issuer    = jwt["Issuer"]   ?? "HealthPlatformApi";
        _audience  = jwt["Audience"] ?? "HealthPlatformClients";
        _cache     = cache;
    }

    // ── Token pair generation ─────────────────────────────────────────────────
    public TokenResult GenerateTokenPair(User user, Guid sessionId)
    {
        var accessToken  = BuildJwt(user, sessionId);
        var refreshToken = GenerateRefreshToken();
        return new TokenResult(accessToken, refreshToken, AccessTokenMinutes * 60, sessionId);
    }

    // ── Refresh token lifecycle ───────────────────────────────────────────────
    public Task StoreRefreshTokenAsync(
        Guid userId, string refreshToken, CancellationToken ct = default)
    {
        var key = RefreshKey(userId);
        return _cache.SetAsync(key, new RefreshEntry(refreshToken), _refreshTtl, ct);
    }

    public async Task<bool> ValidateAndConsumeRefreshTokenAsync(
        Guid userId, string refreshToken, CancellationToken ct = default)
    {
        var key   = RefreshKey(userId);
        var entry = await _cache.GetAsync<RefreshEntry>(key, ct);

        if (entry is null || entry.Token != refreshToken)
            return false;

        // Consume — delete before returning to prevent reuse.
        await _cache.DeleteAsync(key, ct);
        return true;
    }

    public Task RevokeRefreshTokenAsync(Guid userId, CancellationToken ct = default)
        => _cache.DeleteAsync(RefreshKey(userId), ct);

    // ── Private helpers ───────────────────────────────────────────────────────
    private string BuildJwt(User user, Guid sessionId)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now   = DateTime.UtcNow;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role,               user.Role.ToString()),
            new Claim("sid",                         sessionId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer:             _issuer,
            audience:           _audience,
            claims:             claims,
            notBefore:          now,
            expires:            now.AddMinutes(AccessTokenMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(RefreshTokenByteLength);
        return Convert.ToBase64String(bytes);           // 44-char URL-safe string
    }

    private static string RefreshKey(Guid userId) => $"refresh:{userId}";

    // ── Private record for Redis serialisation ────────────────────────────────
    private sealed record RefreshEntry(string Token);
}
```

### 2. Register `JwtTokenService` in `DependencyInjection.cs`

**File:** `src/HealthPlatform.Infrastructure/DependencyInjection.cs`

Add the following using directives (alongside the existing Identity/Interceptors usings):

No additional `using` needed — `JwtTokenService` is in `HealthPlatform.Infrastructure.Security`
which is already accessible within the project.

Locate the Audit infrastructure registration block and append after it:

```csharp
// ── JWT token service ──────────────────────────────────────────────────
services.AddScoped<IJwtTokenService, JwtTokenService>();
```

The full updated section will look like:

```csharp
// ── Audit infrastructure ───────────────────────────────────────────────
services.AddScoped<ICurrentUserService, CurrentUserService>();
services.AddScoped<AuditSaveChangesInterceptor>();

// ── JWT token service ──────────────────────────────────────────────────
services.AddScoped<IJwtTokenService, JwtTokenService>();
```

---

## NuGet Package Note

`System.IdentityModel.Tokens.Jwt` is already present in the solution via
`Microsoft.AspNetCore.Authentication.JwtBearer` (referenced in
`HealthPlatform.Api`). The `HealthPlatform.Infrastructure` project must
reference it explicitly since it doesn't inherit the API project's packages.

Add to `HealthPlatform.Infrastructure.csproj` (inside the existing `<ItemGroup>`
with other `PackageReference` entries):

```xml
<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.3.4" />
```

> **Version:** Match the version transitively used by
> `Microsoft.AspNetCore.Authentication.JwtBearer 8.0.x`. Check with
> `dotnet list package --include-transitive` if unsure. `8.3.4` is correct
> for the .NET 8 era.

---

## Design Notes

### `ICacheService` for Refresh Tokens
Refresh tokens are stored via the existing `ICacheService` abstraction (backed
by Redis `RedisCacheService`). This avoids a direct Redis dependency in
`JwtTokenService` and allows the cache to degrade gracefully (connection errors
return `null` from `GetAsync`, which the validator treats as "token not found").

### `RefreshEntry` Private Record
The Redis value is serialised as `{"token":"<value>"}` via `ICacheService.SetAsync<T>`.
A dedicated `RefreshEntry` record provides a typed wrapper for JSON round-trips.

### `ClaimTypes.Role` vs Custom Claim
`ClaimTypes.Role` (`http://schemas.microsoft.com/ws/2008/06/identity/claims/role`)
is used so that `[Authorize(Roles = "Admin")]` attributes work without additional
claim mapping in `Program.cs`.

---

## Acceptance Checklist

- [ ] `JwtTokenService` created in `Infrastructure/Security/`
- [ ] Access token is HMAC-SHA256 signed with `JwtSettings:SecretKey`
- [ ] JWT contains `sub` (userId), `email`, `role`, `sid` (sessionId), `jti` claims
- [ ] Access token lifetime is 30 minutes (ExpiresIn = 1800)
- [ ] Refresh token is `RandomNumberGenerator`-generated (32 bytes, Base64)
- [ ] Refresh token stored in Redis under key `refresh:{userId}` with 7-day TTL
- [ ] `ValidateAndConsumeRefreshTokenAsync` deletes the key before returning `true`
- [ ] `System.IdentityModel.Tokens.Jwt` package added to Infrastructure `.csproj`
- [ ] `IJwtTokenService` registered as Scoped in `DependencyInjection.cs`
- [ ] Solution builds with 0 errors
