# Task 001: StackExchange.Redis Client Registration with TLS

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-010 |
| **Epic** | EP-DATA |
| **Layer** | Infrastructure |
| **Priority** | Critical |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | None (EP-TECH infrastructure project already in place) |

## Objective

Register an `IConnectionMultiplexer` singleton backed by `StackExchange.Redis`
so every subsequent service (`ISessionStore`, `ICacheService`) injects a single
shared connection to Upstash Redis. TLS must be enabled for Upstash's
`rediss://` endpoints; the connection string is read from an environment
variable so it is never committed.

> **AC-6 already satisfied**: `AddRedis()` is registered in
> `DependencyInjection.cs` from EP-TECH; the `/health` endpoint already checks
> Redis liveness. No health-check work is required in this task.

## Acceptance Criteria Covered

- AC-1: StackExchange.Redis client configured with Upstash Redis connection string
- AC-2: TLS encryption enabled for Redis connections
- AC-8: Redis connection pool configured for concurrent access

## Implementation Steps

### 1. Add NuGet Package to `HealthPlatform.Infrastructure.csproj`

```xml
<PackageReference Include="StackExchange.Redis" Version="2.8.24" />
```

`AspNetCore.HealthChecks.Redis` already present; `StackExchange.Redis` is its
runtime dependency and is being added explicitly for direct use.

### 2. Add Redis Configuration Section to `appsettings.json`

```json
"Redis": {
  "ConnectionString": "localhost:6379",
  "Ssl": false,
  "ConnectTimeout": 5000,
  "SyncTimeout": 1000,
  "AbortOnConnectFail": false
}
```

> **Production / Upstash override**: supply
> `Redis__ConnectionString=rediss://:token@host:port` and `Redis__Ssl=true` via
> environment variable. The `rediss://` scheme automatically implies TLS in
> StackExchange.Redis when `Ssl=true`.

### 3. Add Development Override to `appsettings.Development.json`

```json
"Redis": {
  "ConnectionString": "localhost:6379",
  "Ssl": false
}
```

### 4. Register `IConnectionMultiplexer` in `DependencyInjection.cs`

Add after `AddDbContext<ApplicationDbContext>()`:

```csharp
var redisConfig = ConfigurationOptions.Parse(
    configuration["Redis:ConnectionString"] ?? "localhost:6379");
redisConfig.Ssl               = bool.Parse(configuration["Redis:Ssl"] ?? "false");
redisConfig.ConnectTimeout    = int.Parse(configuration["Redis:ConnectTimeout"] ?? "5000");
redisConfig.SyncTimeout       = int.Parse(configuration["Redis:SyncTimeout"] ?? "1000");
redisConfig.AbortOnConnectFail = bool.Parse(
    configuration["Redis:AbortOnConnectFail"] ?? "false");

services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConfig));
```

### 5. Update the Existing `AddRedis` Health Check Call

The existing health check uses `ConnectionStrings:Redis`. Point it to the new
`Redis:ConnectionString` key so both use the same source of truth:

```csharp
// Before:
.AddRedis(
    configuration.GetConnectionString("Redis") ?? "localhost:6379",
    name: "redis",
    tags: ["cache", "ready"])

// After:
.AddRedis(
    configuration["Redis:ConnectionString"] ?? "localhost:6379",
    name: "redis",
    tags: ["cache", "ready"])
```

Remove the now-unused `ConnectionStrings:Redis` entry from `appsettings.json`.

### 6. Add `using` Directives to `DependencyInjection.cs`

```csharp
using StackExchange.Redis;
```

## Files Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Infrastructure/HealthPlatform.Infrastructure.csproj` | Add `StackExchange.Redis 2.8.24` |
| `src/HealthPlatform.Infrastructure/DependencyInjection.cs` | Register `IConnectionMultiplexer`; update health-check connection string key |
| `src/HealthPlatform.Api/appsettings.json` | Replace `ConnectionStrings.Redis` with `Redis` config section |
| `src/HealthPlatform.Api/appsettings.Development.json` | Add `Redis` dev override |

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Release
dotnet test HealthPlatform.sln --no-build --configuration Release
```

## Notes

- `AbortOnConnectFail = false` is critical: it allows the app to start even
  when Redis is momentarily unavailable, enabling the graceful fallback in
  Task 003.
- `IConnectionMultiplexer` is intentionally `Singleton` — StackExchange.Redis
  manages internal connection pooling; creating multiple multiplexers is an
  anti-pattern.
- The `ConnectionStrings:Redis` key in `appsettings.json` is removed in this
  task to avoid dual-source confusion. The existing `AspNetCore.HealthChecks.Redis`
  registration accepts a raw connection string, which `Redis:ConnectionString`
  satisfies.
