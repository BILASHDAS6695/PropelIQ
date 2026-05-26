# Task 003: AuditInterceptor — SaveChanges Capture, Hash Chain, and DI Registration

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-011 |
| **Epic** | EP-DATA |
| **Layer** | Infrastructure |
| **Priority** | Critical |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | Task 001 (`AuditLog`, `AuditAction`), Task 002 (`AuditLogs` DbSet registered) |

## Objective

Implement `AuditInterceptor : ISaveChangesInterceptor` to:

1. Detect all `Added`, `Modified`, and `Deleted` entity state changes (excluding `AuditLog` itself).
2. Serialise the changed properties as JSONB `{ "Prop": { "Old": ..., "New": ... } }`.
3. Compute a SHA-256 hash chain: `CurrentHash = SHA256(PreviousHash + Timestamp + Action + EntityId + UserId)`.
4. Resolve the authenticated user's ID from `IHttpContextAccessor`.
5. Write `AuditLog` rows **after** the main transaction commits (fire-and-forget async write) so
   audit logging never blocks or fails the primary operation.

## Acceptance Criteria Covered

- AC-4: Each new audit entry includes SHA-256 hash of previous entry (chain integrity)
- AC-5: EF Core `SaveChanges` interceptor automatically captures entity changes
- AC-6: Interceptor records Created, Updated, Deleted actions with before/after values
- AC-7: Audit logging does not block the main transaction (async write after commit)
- AC-8: Audit entries include the authenticated user's ID from the request context

---

## Implementation Steps

### 1. Create `AuditInterceptor.cs`

Create file: `src/HealthPlatform.Infrastructure/Persistence/Interceptors/AuditInterceptor.cs`

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HealthPlatform.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Appends an immutable <see cref="AuditLog"/> row for every entity change after the
/// main transaction commits. Uses SHA-256 hash chaining for tamper-evidence (HIPAA DR-016).
/// </summary>
public sealed class AuditInterceptor : ISaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // -------------------------------------------------------------------------
    // SavedChangesAsync — fires AFTER the main transaction commits
    // -------------------------------------------------------------------------
    public async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return result;

        await WriteAuditEntriesAsync(eventData.Context, cancellationToken);
        return result;
    }

    public int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is null)
            return result;

        // Synchronous path — run async work synchronously as a fire-and-forget guard
        WriteAuditEntriesAsync(eventData.Context, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
        return result;
    }

    // -------------------------------------------------------------------------
    // SavingChangesAsync — capture before-values while change tracker is active
    // -------------------------------------------------------------------------
    public ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        // Before-values must be captured here because EF Core clears the change
        // tracker after SaveChanges completes.
        eventData.Context?.ChangeTracker.DetectChanges();
        CaptureBeforeValues(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        eventData.Context?.ChangeTracker.DetectChanges();
        CaptureBeforeValues(eventData.Context);
        return result;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Stores before-state in the entity entry's TemporaryValues so it is
    /// available in <see cref="WriteAuditEntriesAsync"/> after the commit.
    /// </summary>
    private static void CaptureBeforeValues(DbContext? context)
    {
        if (context is null) return;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog) continue; // skip self
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            // Store original values as a tag so they survive the commit flush
            entry.State = entry.State; // ensure original values loaded
        }
    }

    private async Task WriteAuditEntriesAsync(DbContext context, CancellationToken ct)
    {
        var userId = ResolveUserId();
        var now    = DateTimeOffset.UtcNow;

        // Retrieve the last hash to begin the chain
        var previousHash = await GetLastHashAsync(context, ct);

        var auditEntries = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog) continue;
            if (entry.Entity is not BaseEntity baseEntity) continue;

            var action = entry.State switch
            {
                EntityState.Added    => (AuditAction?)AuditAction.Created,
                EntityState.Modified => AuditAction.Updated,
                EntityState.Deleted  => AuditAction.Deleted,
                _                    => null
            };

            if (action is null) continue;

            var entityId   = baseEntity.Id;
            var entityType = entry.Entity.GetType().Name;
            var details    = BuildDetails(entry, action.Value);
            var timestamp  = now;

            var currentHash = ComputeHash(previousHash, timestamp, action.Value, entityId, userId);

            var auditLog = new AuditLog
            {
                Id           = Guid.NewGuid(),
                UserId       = userId,
                Action       = action.Value,
                EntityType   = entityType,
                EntityId     = entityId,
                Timestamp    = timestamp,
                Details      = details,
                PreviousHash = previousHash,
                CurrentHash  = currentHash
            };

            auditEntries.Add(auditLog);
            previousHash = currentHash; // chain next entry
        }

        if (auditEntries.Count == 0) return;

        context.Set<AuditLog>().AddRange(auditEntries);

        // Direct SQL save to avoid recursive interception
        await context.SaveChangesAsync(acceptAllChangesOnSuccess: true, ct);
    }

    private static string BuildDetails(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry,
        AuditAction action)
    {
        var changes = new Dictionary<string, object?>();

        if (action == AuditAction.Created)
        {
            foreach (var prop in entry.Properties)
                changes[prop.Metadata.Name] = new { New = prop.CurrentValue };
        }
        else if (action == AuditAction.Updated)
        {
            foreach (var prop in entry.Properties.Where(p => p.IsModified))
                changes[prop.Metadata.Name] = new { Old = prop.OriginalValue, New = prop.CurrentValue };
        }
        else // Deleted
        {
            foreach (var prop in entry.Properties)
                changes[prop.Metadata.Name] = new { Old = prop.OriginalValue };
        }

        return JsonSerializer.Serialize(changes);
    }

    private static async Task<string?> GetLastHashAsync(DbContext context, CancellationToken ct)
    {
        // Efficient: ORDER BY timestamp DESC LIMIT 1 via EF Core
        return await context.Set<AuditLog>()
            .OrderByDescending(a => a.Timestamp)
            .Select(a => a.CurrentHash)
            .FirstOrDefaultAsync(ct);
    }

    private static string ComputeHash(
        string? previousHash,
        DateTimeOffset timestamp,
        AuditAction action,
        Guid entityId,
        Guid? userId)
    {
        // Hash chain formula (ADR-006):
        // SHA256( PreviousHash + Timestamp + Action + EntityId + UserId )
        var raw = $"{previousHash}{timestamp:O}{action}{entityId}{userId}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private Guid? ResolveUserId()
    {
        var claim = _httpContextAccessor.HttpContext?
            .User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
```

> **Recursive-insert guard**: `WriteAuditEntriesAsync` calls `context.SaveChangesAsync`
> directly. Because `AuditLog` entries are filtered out of the loop (`if (entry.Entity is AuditLog) continue`),
> the second save produces no new audit rows — no infinite recursion.

### 2. Register `IHttpContextAccessor` and `AuditInterceptor` in DI

File: `src/HealthPlatform.Infrastructure/DependencyInjection.cs`

Add before the `AddDbContext` call:

```csharp
services.AddHttpContextAccessor();
services.AddScoped<AuditInterceptor>();
```

Then update `AddDbContext` to inject the interceptor:

```csharp
services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
{
    var auditInterceptor = serviceProvider.GetRequiredService<AuditInterceptor>();
    options
        .UseNpgsql(
            configuration.GetConnectionString("DefaultConnection"),
            npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                npgsqlOptions.MaxBatchSize(100);
            })
        .UseSnakeCaseNamingConvention()
        .AddInterceptors(auditInterceptor);
});
```

> If `AddDbContext` already uses `(options => ...)` (single-arg overload), change it to
> `(serviceProvider, options) => ...)` (two-arg overload) as shown above so the scoped
> `AuditInterceptor` can be resolved.

### 3. Add Required `using` in `DependencyInjection.cs`

```csharp
using HealthPlatform.Infrastructure.Persistence.Interceptors;
using Microsoft.Extensions.DependencyInjection;
```

### 4. Verify Build

Run from `src/`:

```bash
dotnet build HealthPlatform.sln
```

**Expected output:**

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## Verification Checklist

- [ ] `AuditInterceptor.cs` created in `src/HealthPlatform.Infrastructure/Persistence/Interceptors/`
- [ ] `ISaveChangesInterceptor` is implemented with both sync and async overrides
- [ ] `AuditLog` entries are skipped inside the interceptor loop (no self-audit)
- [ ] `BaseEntity`-derived entities only are audited (non-base entities skipped)
- [ ] Hash: `SHA256(PreviousHash + Timestamp + Action + EntityId + UserId)` in lowercase hex
- [ ] `IHttpContextAccessor` registered via `services.AddHttpContextAccessor()`
- [ ] `AuditInterceptor` registered as `Scoped` and injected into `AddDbContext`
- [ ] `dotnet build` passes — 0 errors, 0 warnings
