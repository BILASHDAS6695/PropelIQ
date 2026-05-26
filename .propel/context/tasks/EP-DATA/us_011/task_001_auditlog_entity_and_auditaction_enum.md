# Task 001: AuditLog Domain Entity and AuditAction Enum

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-011 |
| **Epic** | EP-DATA |
| **Layer** | Domain |
| **Priority** | Critical |
| **Estimated Effort** | 20 minutes |
| **Dependencies** | US-008 complete (`InitialCreate` migration exists; `BaseEntity` is available) |

## Objective

Add the `AuditAction` enum and the `AuditLog` entity to the Domain layer.

`AuditLog` is **not** an `AuditableEntity` — it must never carry soft-delete columns or be
subject to EF Core update tracking. It extends `BaseEntity` directly (only `Id`) so EF Core
has no change-tracking footprint other than inserts.

This is the prerequisite for Task 002's EF Core configuration and Task 003's interceptor.

## Acceptance Criteria Covered

- AC-1: AuditLog table created with: Id, UserId, Action, EntityType, EntityId, Timestamp, Details (JSONB), PreviousHash, CurrentHash

---

## Implementation Steps

### 1. Create `AuditAction` Enum

Create file: `src/HealthPlatform.Domain/Enums/AuditAction.cs`

```csharp
namespace HealthPlatform.Domain.Enums;

public enum AuditAction
{
    Created,
    Updated,
    Deleted
}
```

### 2. Create `AuditLog` Entity

Create file: `src/HealthPlatform.Domain/Entities/AuditLog.cs`

```csharp
using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

/// <summary>
/// Append-only audit record. Never modified or deleted after insert.
/// Hash chain guarantees tamper-evidence (HIPAA DR-016).
/// </summary>
public class AuditLog : BaseEntity
{
    /// <summary>Authenticated user who triggered the change. Null for system operations.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Type of data operation performed.</summary>
    public AuditAction Action { get; set; }

    /// <summary>CLR type name of the entity that changed (e.g. "Appointment").</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Primary key of the changed entity.</summary>
    public Guid EntityId { get; set; }

    /// <summary>UTC timestamp of the audit event.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Changed properties serialised as JSONB: { "PropertyName": { "Old": ..., "New": ... } }
    /// </summary>
    public string Details { get; set; } = "{}";

    /// <summary>CurrentHash of the immediately preceding AuditLog row. Null for the first entry.</summary>
    public string? PreviousHash { get; set; }

    /// <summary>
    /// SHA-256( PreviousHash + Timestamp + Action + EntityId + UserId ).
    /// Verified by compliance tooling to detect tampering.
    /// </summary>
    public string CurrentHash { get; set; } = string.Empty;
}
```

**Why `BaseEntity` and not `AuditableEntity`?**

`AuditableEntity` carries `CreatedAt`, `UpdatedAt`, soft-delete properties, and `UpdateAuditableEntities()`
intercepts its entries on every `SaveChangesAsync`. Extending it would mean the audit interceptor
tries to audit the audit log itself — an infinite loop. `BaseEntity` provides only `Id (Guid)`,
keeping the entity insert-only and free from recursive interception.

### 3. Verify Build

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

- [ ] `AuditAction.cs` created in `src/HealthPlatform.Domain/Enums/` with 3 values: `Created`, `Updated`, `Deleted`
- [ ] `AuditLog.cs` created in `src/HealthPlatform.Domain/Entities/` extending `BaseEntity`
- [ ] `AuditLog` does **not** extend `AuditableEntity` or implement `ISoftDeletable`
- [ ] All 9 properties present: `Id` (inherited), `UserId`, `Action`, `EntityType`, `EntityId`, `Timestamp`, `Details`, `PreviousHash`, `CurrentHash`
- [ ] `dotnet build` passes — 0 errors, 0 warnings
