# Task 002: Audit SaveChanges Interceptor with SHA-256 Hash Chain

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-011 |
| **Epic** | EP-DATA |
| **Layer** | Infrastructure (interceptor) + Infrastructure DI wiring |
| **Priority** | Critical |
| **Estimated Effort** | 60 minutes |
| **Dependencies** | Task 001 (`ICurrentUserService` must be registered) |

## Objective

Implement an EF Core `ISaveChangesInterceptor` that automatically captures
`Created`, `Updated`, and `Deleted` events for every tracked entity (except
`AuditLog` itself) and writes immutable `AuditLog` records with a SHA-256
hash chain in the same database transaction. The interceptor is Scoped so it
can consume the Scoped `ICurrentUserService`.

## Acceptance Criteria Covered

- AC-4: Each new audit entry includes SHA-256 hash of previous entry
- AC-5: `ISaveChangesInterceptor` automatically captures entity changes
- AC-6: Records Created, Updated, Deleted actions with before/after values
- AC-7: Interceptor writes audit logs within the same transaction (atomic, no separate round-trip)
- AC-8: User ID sourced from `ICurrentUserService`

## Implementation Steps

### 1. Create `AuditSaveChangesInterceptor`

Create `src/HealthPlatform.Infrastructure/Persistence/Interceptors/AuditSaveChangesInterceptor.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HealthPlatform.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Automatically captures entity-level changes and writes <see cref="AuditLog"/>
/// entries with a SHA-256 hash chain within the same <see cref="DbContext"/>
/// transaction. Skips logging when no authenticated user is present
/// (e.g., startup seeding, background services).
/// Hash formula: SHA256(previousHash + timestamp(ISO-8601) + action + entityId + userId)
/// </summary>
internal sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;

    public AuditSaveChangesInterceptor(ICurrentUserService currentUser)
        => _currentUser = currentUser;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData    eventData,
        InterceptionResult<int> result,
        CancellationToken     cancellationToken = default)
    {
        if (eventData.Context is null || !_currentUser.IsAuthenticated)
            return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var db      = eventData.Context;
        var userId  = _currentUser.UserId!.Value;
        var entries = BuildAuditEntries(db, userId);

        if (entries.Count > 0)
        {
            var lastHash = await db.Set<AuditLog>()
                .OrderByDescending(a => a.Timestamp)
                .Select(a => a.CurrentHash)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            foreach (var entry in entries)
            {
                var hash = ComputeHash(
                    lastHash, entry.Timestamp, entry.Action,
                    entry.EntityId, entry.UserId);

                entry.PreviousHash = string.IsNullOrEmpty(lastHash) ? null : lastHash;
                entry.CurrentHash  = hash;
                lastHash           = hash;
            }

            await db.Set<AuditLog>().AddRangeAsync(entries, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static List<AuditLog> BuildAuditEntries(DbContext db, Guid userId)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var logs      = new List<AuditLog>();

        foreach (var entry in db.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog) continue;
            if (entry.State is EntityState.Unchanged or EntityState.Detached) continue;

            var action = entry.State switch
            {
                EntityState.Added    => "Created",
                EntityState.Modified => "Updated",
                EntityState.Deleted  => "Deleted",
                _                    => null
            };

            if (action is null) continue;

            var entityId = entry.Properties
                .FirstOrDefault(p => p.Metadata.IsPrimaryKey())
                ?.CurrentValue as Guid? ?? Guid.Empty;

            logs.Add(new AuditLog
            {
                Id         = Guid.NewGuid(),
                UserId     = userId,
                Action     = action,
                EntityType = entry.Entity.GetType().Name,
                EntityId   = entityId,
                Timestamp  = timestamp,
                Details    = BuildDetails(entry, action),
                CurrentHash = string.Empty   // populated by caller
            });
        }

        return logs;
    }

    private static JsonDocument BuildDetails(EntityEntry entry, string action)
    {
        var data = action switch
        {
            "Created" => (object)new
            {
                newValues = entry.Properties
                    .Where(p => !p.Metadata.IsPrimaryKey())
                    .ToDictionary(p => p.Metadata.Name,
                                  p => p.CurrentValue)
            },
            "Updated" => new
            {
                oldValues = entry.Properties
                    .Where(p => p.IsModified)
                    .ToDictionary(p => p.Metadata.Name,
                                  p => p.OriginalValue),
                newValues = entry.Properties
                    .Where(p => p.IsModified)
                    .ToDictionary(p => p.Metadata.Name,
                                  p => p.CurrentValue)
            },
            "Deleted" => new
            {
                oldValues = entry.Properties
                    .Where(p => !p.Metadata.IsPrimaryKey())
                    .ToDictionary(p => p.Metadata.Name,
                                  p => p.OriginalValue)
            },
            _ => new { }
        };

        return JsonDocument.Parse(
            JsonSerializer.Serialize(data,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
    }

    private static string ComputeHash(
        string         previousHash,
        DateTimeOffset timestamp,
        string         action,
        Guid           entityId,
        Guid           userId)
    {
        var input = $"{previousHash}{timestamp:O}{action}{entityId}{userId}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
```

### 2. Update `DependencyInjection.cs` — Register Interceptor and Update `AddDbContext`

The interceptor is Scoped (it depends on `ICurrentUserService` which is Scoped).
EF Core requires interceptors to be provided at context construction time, so
`AddDbContext` must use the `(IServiceProvider sp, DbContextOptionsBuilder options)`
overload.

Modify `src/HealthPlatform.Infrastructure/DependencyInjection.cs`:

**Add using:**
```csharp
using HealthPlatform.Infrastructure.Persistence.Interceptors;
```

**Replace the `AddDbContext` block** (change lambda signature to `(sp, options)`
and add `.AddInterceptors(...)`):

```csharp
services.AddScoped<AuditSaveChangesInterceptor>();

services.AddDbContext<ApplicationDbContext>((sp, options) =>
    options
        .UseNpgsql(
            configuration.GetConnectionString("DefaultConnection"),
            npgsqlOptions => npgsqlOptions.MigrationsAssembly(
                typeof(ApplicationDbContext).Assembly.FullName))
        .UseSnakeCaseNamingConvention()
        .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));
```

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Infrastructure/Persistence/Interceptors/AuditSaveChangesInterceptor.cs` | New — EF Core interceptor with hash chain |
| `src/HealthPlatform.Infrastructure/DependencyInjection.cs` | Register interceptor + `(sp, options)` `AddDbContext` overload |

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Release
dotnet test HealthPlatform.sln --no-build --configuration Release
```

## Notes

- `AuditLog` entities added inside `SavingChangesAsync` are included in the
  same EF Unit of Work and committed atomically with the triggering entities —
  no separate `SaveChanges` call is needed.
- The `AuditLog` skip guard (`if (entry.Entity is AuditLog) continue`) prevents
  infinite recursion: the newly added `AuditLog` entries appear in `ChangeTracker`
  with `EntityState.Added` but are skipped.
- `OrderByDescending(a => a.Timestamp).Select(a => a.CurrentHash)` fetches only
  the last hash — a single lightweight DB query per save.
- **Known limitation**: concurrent saves from different requests could produce
  hash chain forks. A production-hardened solution would use a PostgreSQL
  advisory lock or a `SERIAL`-based sequence column. This is acceptable for
  the current compliance scope.
- `Convert.ToHexString(...).ToLowerInvariant()` produces a 64-character lowercase
  hex string matching the `HasMaxLength(64)` constraint in `AuditLogConfiguration`.
- `(sp, options)` overload is required to resolve the Scoped interceptor from
  the container. Without it, DI cannot inject `ICurrentUserService` into the
  interceptor at context-creation time.
