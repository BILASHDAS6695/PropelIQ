# Task 002: Global Soft-Delete Query Filter and PostgreSQL Concurrency Tokens

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-009 |
| **Epic** | EP-DATA |
| **Layer** | Infrastructure |
| **Priority** | Critical |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | Task 001 complete (`ISoftDeletable` on `AuditableEntity`) |

## Objective

Wire up three infrastructure-layer behaviours that rely on `ISoftDeletable`:

1. **Global query filter** — EF Core automatically appends `WHERE is_deleted = false` to every
   query against a soft-deletable entity, with no per-query changes required by callers.
2. **Soft-delete interception** — `SaveChangesAsync` intercepts `EntityState.Deleted` for any
   `ISoftDeletable` entity, converts it to `EntityState.Modified`, and stamps the three
   soft-delete columns (`IsDeleted`, `DeletedAt`, `DeletedBy`).
3. **Concurrency tokens** — `Appointment` and `PatientView360` use PostgreSQL's native `xmin`
   system column as the concurrency token, preventing lost-update races with zero schema overhead.

## Acceptance Criteria Covered

- AC-7: Soft-delete filter configured globally (`IsDeleted = false` query filter) — **implementation step**

---

## Implementation Steps

### 1. Update `ApplicationDbContext.OnModelCreating` — Global Query Filter

File: `src/HealthPlatform.Infrastructure/Persistence/ApplicationDbContext.cs`

Add the `System.Linq.Expressions` using directive at the top and insert the query-filter loop
**after** the UTC converter loop and **before** `base.OnModelCreating(modelBuilder)`:

```csharp
using System.Linq.Expressions;
using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
```

Inside `OnModelCreating`, immediately before the `base.OnModelCreating(modelBuilder)` call:

```csharp
// Global soft-delete query filter for all ISoftDeletable entities
foreach (var entityType in modelBuilder.Model.GetEntityTypes())
{
    if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
        continue;

    var parameter = Expression.Parameter(entityType.ClrType, "e");
    var isDeletedProperty = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
    var notDeleted = Expression.Not(isDeletedProperty);
    var lambda = Expression.Lambda(notDeleted, parameter);
    entityType.SetQueryFilter(lambda);
}
```

### 2. Update `SaveChangesAsync` — Soft-Delete Interception

Replace the existing `SaveChangesAsync` override with one that intercepts hard deletes on
`ISoftDeletable` entities before forwarding to EF Core:

```csharp
public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    InterceptSoftDeletes();
    UpdateAuditableEntities();
    return base.SaveChangesAsync(cancellationToken);
}

private void InterceptSoftDeletes()
{
    var deletedEntries = ChangeTracker
        .Entries<ISoftDeletable>()
        .Where(e => e.State == EntityState.Deleted);

    foreach (var entry in deletedEntries)
    {
        entry.State = EntityState.Modified;
        entry.Entity.IsDeleted = true;
        entry.Entity.DeletedAt = DateTimeOffset.UtcNow;
        // DeletedBy will be populated by the application service layer when a current-user
        // service is available; left null here to keep Infrastructure free of HTTP concerns.
        entry.Entity.DeletedBy = null;
    }
}
```

> **Order matters**: `InterceptSoftDeletes()` must run before `UpdateAuditableEntities()` so
> that the converted `Modified` entries pick up `UpdatedAt` stamping.

### 3. Add `xmin` Concurrency Tokens

`xmin` is a PostgreSQL system column (uint32) that EF Core / Npgsql expose as a zero-cost
concurrency token — no migration column needed.

**File: `src/HealthPlatform.Infrastructure/Persistence/Configurations/AppointmentConfiguration.cs`**

Add one line inside `Configure` (after the existing FK declarations):

```csharp
// PostgreSQL xmin system column as optimistic-concurrency token
builder.UseXminAsConcurrencyToken();
```

**File: `src/HealthPlatform.Infrastructure/Persistence/Configurations/PatientView360Configuration.cs`**

Add the same line at the end of `Configure`:

```csharp
// PostgreSQL xmin system column as optimistic-concurrency token
builder.UseXminAsConcurrencyToken();
```

> `UseXminAsConcurrencyToken()` is provided by `Npgsql.EntityFrameworkCore.PostgreSQL` which is
> already referenced — no new package required.

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

## Key Design Decisions

| Decision | Rationale |
|---|---|
| Filter loop uses `Expression` API | EF Core requires strongly-typed lambdas per entity; the loop generates them dynamically without boxing |
| `InterceptSoftDeletes` before `UpdateAuditableEntities` | Ensures soft-deleted AuditableEntity rows also get `UpdatedAt` stamped on their final `Modified` save |
| `xmin` instead of a RowVersion column | No DDL column needed; PostgreSQL updates `xmin` on every row change automatically; no migration required for this token |
| `DeletedBy = null` in DbContext | Populating the current user requires HTTP context; that concern belongs in the Application layer command handlers, not the Infrastructure DbContext |

---

## Verification Checklist

- [ ] `using System.Linq.Expressions;` added to `ApplicationDbContext.cs`
- [ ] Global query filter loop added to `OnModelCreating` before `base.OnModelCreating`
- [ ] `InterceptSoftDeletes()` private method added and called first in `SaveChangesAsync`
- [ ] `AppointmentConfiguration.cs` has `builder.UseXminAsConcurrencyToken()`
- [ ] `PatientView360Configuration.cs` has `builder.UseXminAsConcurrencyToken()`
- [ ] `dotnet build` passes — 0 errors, 0 warnings
