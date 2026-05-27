# Task 001: Domain Layer — Extend SlotSwapRequest for Staff Mediation

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-030 |
| **Epic** | EP-003 |
| **Layer** | Domain (entity, enum, EF configuration, DB migration) |
| **Priority** | Low |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | US-028 Task 001 (`SlotSwapRequest` entity, `SlotSwapStatus` enum, `SlotSwapRequestConfiguration`) |

## Objective

Extend the domain model to support staff-mediated swap operations:

1. **Add three new `SlotSwapStatus` values** — `StaffApproved`, `StaffDeclined`,
   and `StaffReassigned` — to distinguish staff-initiated outcomes from patient-driven ones.

2. **Add mediation fields to `SlotSwapRequest`** — `OverrideReason`, `MediatedByUserId`,
   `OverriddenAt`, and `ThreeWayNewTargetSlotId` — to capture the context of every staff
   override action for the audit trail.

3. **Add a concurrency token (`Version`)** — guards against two staff members mediating
   the same swap simultaneously (optimistic concurrency edge case from US-030).

4. **Update the EF configuration** — map new properties and the concurrency token.

5. **Apply a DB migration** — add columns to `slot_swap_requests`.

## Acceptance Criteria Covered

- AC: Staff can force-approve/force-decline/initiate three-way swap (new statuses support these flows)
- AC: Override actions logged in audit trail with staff ID and reason (`MediatedByUserId`, `OverrideReason`)
- Edge case: Multiple staff try to mediate same swap → optimistic concurrency check (`Version` token)

---

## Implementation Steps

### 1. Extend `SlotSwapStatus` Enum

Edit `src/HealthPlatform.Domain/Enums/SlotSwapStatus.cs`:

```csharp
namespace HealthPlatform.Domain.Enums;

public enum SlotSwapStatus
{
    Pending          = 0,  // Awaiting target patient response
    Accepted         = 1,  // Target patient accepted (US-029)
    Declined         = 2,  // Target patient declined (US-029)
    Cancelled        = 3,  // Requester cancelled the request
    Expired          = 4,  // No response within 24 hours (US-029)
    StaffApproved    = 5,  // Staff force-approved, bypassing target patient (US-030)
    StaffDeclined    = 6,  // Staff force-declined with mandatory reason (US-030)
    StaffReassigned  = 7,  // Staff performed three-way slot reassignment (US-030)
}
```

> The `HasConversion<string>()` in the EF config means the new string values
> (`"StaffApproved"`, `"StaffDeclined"`, `"StaffReassigned"`) must fit within the
> existing `HasMaxLength(20)` — all three are ≤ 16 characters, so no schema change
> to the column length is needed.

---

### 2. Add Mediation Fields to `SlotSwapRequest`

Edit `src/HealthPlatform.Domain/Entities/SlotSwapRequest.cs`.

Add the following properties after the existing `CancellationReason` property and
before the navigation properties block:

```csharp
// ── Staff mediation fields (US-030) ───────────────────────────────

/// <summary>
/// Mandatory reason text supplied by staff for any override action
/// (force-approve, force-decline, or three-way reassignment).
/// Null for patient-driven outcomes.
/// </summary>
public string? OverrideReason { get; set; }

/// <summary>
/// User ID of the staff member who performed the override.
/// Null for patient-driven outcomes.
/// </summary>
public Guid? MediatedByUserId { get; set; }

/// <summary>
/// UTC timestamp when the staff override was applied.
/// Null for patient-driven outcomes.
/// </summary>
public DateTimeOffset? OverriddenAt { get; set; }

/// <summary>
/// For three-way reassignment only: the new <see cref="AppointmentSlot"/> ID
/// assigned to the target patient after the requester takes the target's
/// original slot. Null for all other swap outcomes.
/// </summary>
public Guid? ThreeWayNewTargetSlotId { get; set; }

// ── Optimistic concurrency token (US-030 edge case) ───────────────

/// <summary>
/// PostgreSQL <c>xmin</c>-backed concurrency token. Automatically incremented
/// by the database on every row update. Prevents two staff members from
/// mediating the same swap request simultaneously.
/// </summary>
public uint Version { get; set; }
```

Full updated file for reference:

```csharp
using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

/// <summary>
/// Represents a patient's request to swap their appointment slot with another
/// patient's booked slot at the same provider.
///
/// Privacy rule: the requester never sees the target patient's identity —
/// only the target slot time is exposed.
/// </summary>
public class SlotSwapRequest : AuditableEntity
{
    /// <summary>Patient profile ID of the patient who initiated the swap.</summary>
    public Guid RequesterPatientId { get; set; }

    /// <summary>The requester's current appointment (the slot they are offering).</summary>
    public Guid RequesterAppointmentId { get; set; }

    /// <summary>The target appointment the requester wants to acquire.</summary>
    public Guid TargetAppointmentId { get; set; }

    /// <summary>Current status of the swap request.</summary>
    public SlotSwapStatus Status { get; set; } = SlotSwapStatus.Pending;

    /// <summary>UTC timestamp when the request auto-expires (creation + 24 h).</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Optional reason recorded when the request is cancelled or declined.</summary>
    public string? CancellationReason { get; set; }

    // ── Staff mediation fields (US-030) ───────────────────────────────────

    /// <summary>
    /// Mandatory reason text supplied by staff for any override action.
    /// Null for patient-driven outcomes.
    /// </summary>
    public string? OverrideReason { get; set; }

    /// <summary>
    /// User ID of the staff member who performed the override.
    /// Null for patient-driven outcomes.
    /// </summary>
    public Guid? MediatedByUserId { get; set; }

    /// <summary>
    /// UTC timestamp when the staff override was applied.
    /// Null for patient-driven outcomes.
    /// </summary>
    public DateTimeOffset? OverriddenAt { get; set; }

    /// <summary>
    /// For three-way reassignment: the new slot ID assigned to the target patient.
    /// Null for all other swap outcomes.
    /// </summary>
    public Guid? ThreeWayNewTargetSlotId { get; set; }

    // ── Optimistic concurrency token ──────────────────────────────────────

    /// <summary>
    /// PostgreSQL xmin-backed concurrency token. Prevents simultaneous staff mediation.
    /// </summary>
    public uint Version { get; set; }

    // ── Navigation properties ──────────────────────────────────────────────
    public PatientProfile RequesterPatient     { get; set; } = null!;
    public Appointment    RequesterAppointment { get; set; } = null!;
    public Appointment    TargetAppointment    { get; set; } = null!;
}
```

---

### 3. Update `SlotSwapRequestConfiguration`

Edit `src/HealthPlatform.Infrastructure/Persistence/Configurations/SlotSwapRequestConfiguration.cs`.

Add the following property mappings inside `Configure`, after the existing
`CancellationReason` property configuration and before the index definitions:

```csharp
builder.Property(r => r.OverrideReason)
    .HasMaxLength(500);

builder.Property(r => r.OverriddenAt)
    .IsRequired(false);

builder.Property(r => r.MediatedByUserId)
    .IsRequired(false);

builder.Property(r => r.ThreeWayNewTargetSlotId)
    .IsRequired(false);

// Optimistic concurrency token — maps to PostgreSQL's xmin system column.
// EF Core reads xmin on load and includes it in the WHERE clause of UPDATE
// statements, causing DbUpdateConcurrencyException if the row was modified
// by another transaction between load and save.
builder.UseXminAsConcurrencyToken();
```

> `UseXminAsConcurrencyToken()` is available via the
> `Microsoft.EntityFrameworkCore.PostgreSQL` (Npgsql) package already referenced
> in `HealthPlatform.Infrastructure.csproj`. No additional NuGet package required.
> The `Version` property in the entity must be declared as `public uint Version { get; set; }`
> for the `xmin` mapping to work correctly.

---

### 4. Apply DB Migration

Edit `infra/postgres/migrations.sql`.

Append the following migration block at the end of the file:

```sql
-- ============================================================
-- US-030: Staff Swap Mediation — extend slot_swap_requests
-- ============================================================

ALTER TABLE slot_swap_requests
    ADD COLUMN IF NOT EXISTS override_reason        VARCHAR(500),
    ADD COLUMN IF NOT EXISTS mediated_by_user_id    UUID,
    ADD COLUMN IF NOT EXISTS overridden_at          TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS three_way_new_target_slot_id UUID;

-- Foreign key: staff user who performed the override
ALTER TABLE slot_swap_requests
    ADD CONSTRAINT fk_slot_swap_requests_mediated_by_user
    FOREIGN KEY (mediated_by_user_id)
    REFERENCES users (id)
    ON DELETE RESTRICT;

-- Note: xmin concurrency token is a built-in PostgreSQL system column —
-- no ALTER TABLE required. EF Core/Npgsql reads it automatically.

COMMENT ON COLUMN slot_swap_requests.override_reason IS
    'Mandatory reason text for staff force-approve, force-decline, or three-way reassignment.';
COMMENT ON COLUMN slot_swap_requests.mediated_by_user_id IS
    'User ID of the staff member who performed the override action.';
COMMENT ON COLUMN slot_swap_requests.overridden_at IS
    'UTC timestamp when the staff override was applied.';
COMMENT ON COLUMN slot_swap_requests.three_way_new_target_slot_id IS
    'For three-way reassignment: new slot ID assigned to the target patient.';
```

---

## Files Modified

| Action | Path |
|--------|------|
| EDIT   | `src/HealthPlatform.Domain/Enums/SlotSwapStatus.cs` |
| EDIT   | `src/HealthPlatform.Domain/Entities/SlotSwapRequest.cs` |
| EDIT   | `src/HealthPlatform.Infrastructure/Persistence/Configurations/SlotSwapRequestConfiguration.cs` |
| EDIT   | `infra/postgres/migrations.sql` |

## Verification

- `dotnet build src/HealthPlatform.sln` → 0 errors, 0 warnings related to these changes
- `SlotSwapStatus.StaffApproved`, `StaffDeclined`, `StaffReassigned` resolve correctly
- New columns appear in `slot_swap_requests` after applying the SQL migration
- A `DbUpdateConcurrencyException` is thrown when two EF contexts update the same
  `SlotSwapRequest` row without reloading (xmin mismatch)
