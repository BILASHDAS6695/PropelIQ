# Task 001: ISoftDeletable Interface and AuditableEntity Implementation

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-009 |
| **Epic** | EP-DATA |
| **Layer** | Domain |
| **Priority** | Critical |
| **Estimated Effort** | 20 minutes |
| **Dependencies** | US-008 complete (all 15 entities exist, `InitialCreate` migration generated) |

## Objective

Introduce the `ISoftDeletable` contract into the Domain layer and attach it to `AuditableEntity`
so that all six auditable entities (User, PatientProfile, Provider, Appointment, IntakeRecord,
ClinicalDocument) automatically carry the three soft-delete columns (`IsDeleted`, `DeletedAt`,
`DeletedBy`) without touching any entity file individually.

This single-point change is the prerequisite for Task 002's global query filter and
`SaveChangesAsync` interception.

## Acceptance Criteria Covered

- AC-7: Soft-delete filter configured globally (`IsDeleted = false` query filter) — **foundation step**

---

## Implementation Steps

### 1. Create `ISoftDeletable` Interface

Create file: `src/HealthPlatform.Domain/Common/ISoftDeletable.cs`

```csharp
namespace HealthPlatform.Domain.Common;

public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
    string? DeletedBy { get; set; }
}
```

### 2. Implement `ISoftDeletable` on `AuditableEntity`

File: `src/HealthPlatform.Domain/Common/AuditableEntity.cs`

Add the interface and its three properties:

```csharp
namespace HealthPlatform.Domain.Common;

public abstract class AuditableEntity : BaseEntity, ISoftDeletable
{
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    // ISoftDeletable
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}
```

**Why `AuditableEntity` and not `BaseEntity`?**
The six entities that need soft-delete (User, PatientProfile, Provider, Appointment, IntakeRecord,
ClinicalDocument) all extend `AuditableEntity`. Entities extending `BaseEntity` directly
(AuditLog, Notification, ExtractedData, etc.) are either immutable audit trails or child
aggregates that should be hard-deleted with their parent — so they do not need the interface.

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

No entity or configuration files need changes — the new properties are inherited automatically.

---

## Verification Checklist

- [ ] `ISoftDeletable.cs` created in `src/HealthPlatform.Domain/Common/`
- [ ] `AuditableEntity` implements `ISoftDeletable` and has `IsDeleted`, `DeletedAt`, `DeletedBy`
- [ ] `dotnet build` passes — 0 errors, 0 warnings
- [ ] No individual entity files modified (inheritance handles it)
