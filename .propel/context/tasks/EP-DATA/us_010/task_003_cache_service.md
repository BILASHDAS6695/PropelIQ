# Task 003: Generic Cache Service with Graceful Fallback

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

Define a generic cache abstraction in the Application layer and implement it
in Infrastructure. The cache serialises values to JSON via `System.Text.Json`
and wraps every Redis call in a try/catch: if Redis is unavailable the
operation logs a warning and returns a safe default rather than propagating an
exception. This satisfies the graceful-fallback requirement (AC-7).

## Acceptance Criteria Covered

- AC-5: Cache interface: `Get<T>`, `Set<T>`, `Delete`, `Exists` with configurable TTL
- AC-7: Graceful fallback if Redis is temporarily unavailable (log warning, bypass cache)

> **AC-6 (health check)** is already satisfied by `AddRedis()` registered in
> `DependencyInjection.cs` from EP-TECH. No changes required.

## Implementation Steps

### 1. Create `ICacheService` Interface in Application Layer

Create `src/HealthPlatform.Application/Interfaces/ICacheService.cs`:

```csharp
namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Generic distributed cache service.
/// Implementations must degrade gracefully when the cache is unavailable.
/// Key naming convention: cache:{entityType}:{id}
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Retrieves a cached value. Returns <c>null</c> if the key does not exist
    /// or the cache is unavailable.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;

    /// <summary>Stores a value with the specified TTL. No-ops if cache is unavailable.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
        where T : class;

    /// <summary>Removes the key. No-ops if cache is unavailable.</summary>
    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Returns <c>true</c> if the key exists. Returns <c>false</c> if the cache
    /// is unavailable (fail-safe default).
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}
```

### 2. Create `RedisCacheService` in Infrastructure Layer

Create `src/HealthPlatform.Infrastructure/Cache/RedisCacheService.cs`:

```csharp
using System.Text.Json;
using HealthPlatform.Application.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace HealthPlatform.Infrastructure.Cache;

/// <summary>
/// Redis-backed implementation of <see cref="ICacheService"/>.
/// All operations catch <see cref="RedisException"/> and degrade gracefully:
/// Get returns null, Set/Delete are silent no-ops, Exists returns false.
/// </summary>
internal sealed class RedisCacheService : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IDatabase _db;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer multiplexer,
                              ILogger<RedisCacheService> logger)
    {
        _db     = multiplexer.GetDatabase();
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        where T : class
    {
        try
        {
            var value = await _db.StringGetAsync(key);
            if (value.IsNullOrEmpty) return null;
            return JsonSerializer.Deserialize<T>(value.ToString(), JsonOptions);
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Redis unavailable on Get for key {Key}. Bypassing cache", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl,
                                   CancellationToken ct = default)
        where T : class
    {
        try
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            await _db.StringSetAsync(key, json, ttl);
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Redis unavailable on Set for key {Key}. Skipping cache write", key);
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _db.KeyDeleteAsync(key);
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Redis unavailable on Delete for key {Key}. Skipping", key);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            return await _db.KeyExistsAsync(key);
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Redis unavailable on Exists for key {Key}. Returning false", key);
            return false;
        }
    }
}
```

### 3. Register `ICacheService` in `DependencyInjection.cs`

Add after `ISessionStore` registration:

```csharp
services.AddSingleton<ICacheService, RedisCacheService>();
```

`Singleton` is appropriate — `RedisCacheService` is stateless beyond the shared
`IDatabase` reference which itself is thread-safe.

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Application/Interfaces/ICacheService.cs` | New — generic cache interface |
| `src/HealthPlatform.Infrastructure/Cache/RedisCacheService.cs` | New — Redis implementation with graceful fallback |
| `src/HealthPlatform.Infrastructure/DependencyInjection.cs` | Register `ICacheService` → `RedisCacheService` |

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Release
dotnet test HealthPlatform.sln --no-build --configuration Release
```

## Notes

- Only `RedisException` is caught — programming errors (`ArgumentNullException`,
  `JsonException`) are intentionally allowed to propagate so they surface as
  bugs during development rather than being silently swallowed.
- `JsonNamingPolicy.CamelCase` aligns serialised cache values with the API
  response format, avoiding confusion when debugging cached payloads.
- Cache key convention `cache:{entityType}:{id}` (e.g., `cache:provider:abc123`)
  is enforced by callers, not by the service, to keep the service generic.
- `IDatabase` from `IConnectionMultiplexer.GetDatabase()` is a lightweight
  proxy — it is safe to store as a field in a singleton.
- `System.Text.Json` is used (not `Newtonsoft.Json`) to stay consistent with
  ASP.NET Core's default serialiser and avoid an extra dependency.
