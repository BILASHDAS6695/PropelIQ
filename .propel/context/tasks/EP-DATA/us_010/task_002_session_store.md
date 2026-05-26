# Task 002: Session Store Interface and Redis Implementation

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-010 |
| **Epic** | EP-DATA |
| **Layer** | Application (interface) + Infrastructure (implementation) |
| **Priority** | Critical |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | Task 001 (IConnectionMultiplexer must be registered) |

## Objective

Define a session store abstraction in the Application layer and implement it
in Infrastructure using Redis. The session store supports the 15-minute
inactivity timeout required by NFR-013 through a sliding-expiration `RefreshTTL`
method. Sessions are keyed by user ID (`session:{userId}`).

## Acceptance Criteria Covered

- AC-3: Session store interface: `SetSession`, `GetSession`, `DeleteSession`, `RefreshTTL`
- AC-4: Session TTL set to 15 minutes (900 seconds) with sliding expiration

## Implementation Steps

### 1. Create `ISessionStore` Interface in Application Layer

Create `src/HealthPlatform.Application/Interfaces/ISessionStore.cs`:

```csharp
namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Manages user session tokens in the distributed cache.
/// Sessions expire after 15 minutes of inactivity (sliding expiration).
/// </summary>
public interface ISessionStore
{
    /// <summary>Stores a session value for the given user. Overwrites if exists.</summary>
    Task SetSessionAsync(string userId, string sessionValue, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the session value for the given user.
    /// Returns <c>null</c> if the session has expired or does not exist.
    /// </summary>
    Task<string?> GetSessionAsync(string userId, CancellationToken ct = default);

    /// <summary>Removes the session for the given user (logout / invalidation).</summary>
    Task DeleteSessionAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Resets the TTL to 15 minutes (sliding window). Call on every authenticated
    /// request to keep the session alive during active use.
    /// </summary>
    Task RefreshTtlAsync(string userId, CancellationToken ct = default);
}
```

### 2. Create `RedisSessionStore` in Infrastructure Layer

Create `src/HealthPlatform.Infrastructure/Cache/RedisSessionStore.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace HealthPlatform.Infrastructure.Cache;

/// <summary>
/// Redis-backed implementation of <see cref="ISessionStore"/>.
/// Key format: session:{userId}
/// TTL: 15 minutes (sliding — reset on every authenticated request via RefreshTtlAsync).
/// </summary>
internal sealed class RedisSessionStore : ISessionStore
{
    private static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(15);
    private readonly IDatabase _db;
    private readonly ILogger<RedisSessionStore> _logger;

    public RedisSessionStore(IConnectionMultiplexer multiplexer,
                             ILogger<RedisSessionStore> logger)
    {
        _db     = multiplexer.GetDatabase();
        _logger = logger;
    }

    public async Task SetSessionAsync(string userId, string sessionValue,
                                      CancellationToken ct = default)
    {
        var key = SessionKey(userId);
        await _db.StringSetAsync(key, sessionValue, SessionTtl);
        _logger.LogDebug("Session set for user {UserId}, TTL={Ttl}", userId, SessionTtl);
    }

    public async Task<string?> GetSessionAsync(string userId,
                                               CancellationToken ct = default)
    {
        var value = await _db.StringGetAsync(SessionKey(userId));
        return value.IsNullOrEmpty ? null : value.ToString();
    }

    public async Task DeleteSessionAsync(string userId,
                                         CancellationToken ct = default)
    {
        await _db.KeyDeleteAsync(SessionKey(userId));
        _logger.LogDebug("Session deleted for user {UserId}", userId);
    }

    public async Task RefreshTtlAsync(string userId,
                                      CancellationToken ct = default)
    {
        await _db.KeyExpireAsync(SessionKey(userId), SessionTtl);
    }

    private static RedisKey SessionKey(string userId) => $"session:{userId}";
}
```

### 3. Register `ISessionStore` in `DependencyInjection.cs`

Add after the `IConnectionMultiplexer` registration:

```csharp
services.AddScoped<ISessionStore, RedisSessionStore>();
```

`Scoped` is appropriate — one session interaction per HTTP request lifecycle.

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Application/Interfaces/ISessionStore.cs` | New — session store interface |
| `src/HealthPlatform.Infrastructure/Cache/RedisSessionStore.cs` | New — Redis implementation |
| `src/HealthPlatform.Infrastructure/DependencyInjection.cs` | Register `ISessionStore` → `RedisSessionStore` |

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Release
dotnet test HealthPlatform.sln --no-build --configuration Release
```

## Notes

- `RefreshTtlAsync` uses `KeyExpireAsync` which resets the TTL without fetching
  or re-serializing the value — minimal Redis round-trip for every authenticated request.
- `internal sealed` on `RedisSessionStore` enforces the Clean Architecture
  rule: no code outside Infrastructure references the concrete type.
- `ILogger<RedisSessionStore>` is injected for observability; debug-level logs
  avoid log noise in production.
- Key format `session:{userId}` uses a colon prefix as per Redis key-space
  convention, enabling namespace-level monitoring and key-space notifications.
