# Task 001: Domain Changes + EF Configuration + Migration

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-022 |
| **Epic** | EP-002 |
| **Layer** | Domain + Infrastructure (EF config + migration) |
| **Priority** | High |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | US-021 Task 001 (`AppointmentStatus.Cancelled` must exist, `Appointment.SlotId` nullable) |

## Objective

Add the cancellation-reason vocabulary to the domain: a `CancellationReason`
enum that matches the UI dropdown, two nullable columns on `Appointment`
(`CancellationReason` and `CancellationNote`), an EF column mapping, and the
corresponding database migration.  The audit log entry for every cancellation is
captured automatically by the existing `AuditSaveChangesInterceptor` — no
explicit logging code is required in this task.

## Acceptance Criteria Covered

- AC: Cancellation reason required (dropdown: schedule conflict, feeling better, other)
- AC: Audit log entry for cancellation/reschedule with reason *(interceptor auto-captures the Appointment mutation)*

---

## Implementation Steps

### 1. Add `CancellationReason` Enum

Create `src/HealthPlatform.Domain/Enums/CancellationReason.cs`:

```csharp
namespace HealthPlatform.Domain.Enums;

/// <summary>
/// Reason a patient or staff member provided when cancelling or rescheduling
/// an appointment.  Matches the dropdown values shown in the UI.
/// </summary>
public enum CancellationReason
{
    ScheduleConflict = 0,
    FeelingBetter    = 1,
    Other            = 2
}
```

---

### 2. Add Cancellation Fields to `Appointment`

Edit `src/HealthPlatform.Domain/Entities/Appointment.cs` — append two nullable
properties after `ArrivalTime`:

```csharp
public class Appointment : AuditableEntity
{
    public Guid   PatientId      { get; set; }
    public Guid   ProviderId     { get; set; }
    public Guid?  SlotId         { get; set; }
    public DateTimeOffset  SlotTime { get; set; }
    public AppointmentStatus Status { get; set; }
    public Guid?  PreferredSlotId { get; set; }
    public bool   IsWalkIn        { get; set; }
    public string? VisitReason    { get; set; }
    public int?   QueuePosition   { get; set; }
    public DateTimeOffset? ArrivalTime { get; set; }

    // ── Cancellation ──────────────────────────────────────────────────────
    /// <summary>Populated when Status is Cancelled or when rescheduled.</summary>
    public CancellationReason? CancellationReason { get; set; }
    /// <summary>Optional free-text note; required by the UI when Reason = Other.</summary>
    public string? CancellationNote { get; set; }

    public PatientProfile   Patient  { get; set; } = null!;
    public Provider         Provider { get; set; } = null!;
    public AppointmentSlot? Slot     { get; set; }
    public IntakeRecord? IntakeRecord { get; set; }
    public PreferredSlotPreference? PreferredSlotPreference { get; set; }
    public ICollection<Notification> Notifications { get; set; } = [];
}
```

---

### 3. Update `AppointmentConfiguration`

Edit `src/HealthPlatform.Infrastructure/Persistence/Configurations/AppointmentConfiguration.cs` —
add two property mappings inside `Configure`, after the existing `VisitReason` mapping:

```csharp
// Store enum as string so the DB column is human-readable.
builder.Property(a => a.CancellationReason)
    .HasConversion<string>()
    .HasMaxLength(30);

builder.Property(a => a.CancellationNote).HasMaxLength(500);
```

---

### 4. Add EF Migration

Run from the repository root:

```powershell
dotnet ef migrations add AddCancellationFields `
    --project src/HealthPlatform.Infrastructure `
    --startup-project src/HealthPlatform.Api
```

Verify the generated migration contains `add_column :cancellation_reason` (nullable
`character varying(30)`) and `add_column :cancellation_note` (nullable
`character varying(500)`) on the `appointments` table with no other changes.

Apply the migration:

```powershell
dotnet ef database update `
    --project src/HealthPlatform.Infrastructure `
    --startup-project src/HealthPlatform.Api
```

---

## Verification Checklist

- [ ] `CancellationReason` enum exists with three members matching the UI dropdown labels
- [ ] `Appointment.CancellationReason` is nullable (`CancellationReason?`)
- [ ] `Appointment.CancellationNote` is nullable (`string?`) with `HasMaxLength(500)` in EF config
- [ ] Migration file contains only the two new columns — no unintended renames or drops
- [ ] `dotnet build src/HealthPlatform.sln` compiles without errors
- [ ] `dotnet ef database update` applies cleanly against the local DB
