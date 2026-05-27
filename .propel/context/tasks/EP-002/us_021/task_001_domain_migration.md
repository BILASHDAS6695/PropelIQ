# Task 001: Domain Changes + EF Configuration + Migration

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-021 |
| **Epic** | EP-002 |
| **Layer** | Domain + Infrastructure (EF config + migration) |
| **Priority** | High |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | US-020 Task 001 (`AppointmentStatus.Scheduled` must exist) |

## Objective

Extend the domain to support walk-in appointments: add a `WalkIn` status,
make `SlotId` optional (walk-ins have no pre-booked slot), add `QueuePosition`
and `ArrivalTime` fields, and update the EF configuration to reflect these
nullable relationships and the filtered unique index on `slot_id`.

## Acceptance Criteria Covered

- AC: Walk-in appointment gets status "WalkIn" and queue position assigned
- AC: Walk-in appointment does not consume a pre-defined slot
- AC: Walk-in marked with arrival time (auto-set to current time)

---

## Implementation Steps

### 1. Add `WalkIn` to `AppointmentStatus`

Edit `src/HealthPlatform.Domain/Enums/AppointmentStatus.cs`:

```csharp
namespace HealthPlatform.Domain.Enums;

public enum AppointmentStatus
{
    Scheduled = 0,   // Initial state: booked online, not yet checked in
    Booked    = 1,   // Confirmed / checked in at clinic
    Arrived   = 2,
    Completed = 3,
    Cancelled = 4,
    NoShow    = 5,
    WalkIn    = 6    // Unscheduled walk-in; uses QueuePosition instead of SlotId
}
```

---

### 2. Update `Appointment` Entity

Edit `src/HealthPlatform.Domain/Entities/Appointment.cs`:

- Make `SlotId` nullable — walk-ins have no pre-booked slot.
- Make `Slot` navigation property optional.
- Add `QueuePosition (int?)` — position in provider's daily walk-in queue.
- Add `ArrivalTime (DateTimeOffset?)` — auto-set to UTC now at registration.

```csharp
public class Appointment : AuditableEntity
{
    public Guid   PatientId   { get; set; }
    public Guid   ProviderId  { get; set; }
    public Guid?  SlotId      { get; set; }     // null for walk-in appointments
    public DateTimeOffset  SlotTime      { get; set; }
    public AppointmentStatus Status      { get; set; }
    public Guid?  PreferredSlotId        { get; set; }
    public bool   IsWalkIn               { get; set; }
    public string? VisitReason           { get; set; }
    public int?   QueuePosition          { get; set; }  // walk-in queue order
    public DateTimeOffset? ArrivalTime   { get; set; }  // auto-set at registration

    public PatientProfile   Patient   { get; set; } = null!;
    public Provider         Provider  { get; set; } = null!;
    public AppointmentSlot? Slot      { get; set; }     // null for walk-ins
    public IntakeRecord?    IntakeRecord { get; set; }
    public PreferredSlotPreference? PreferredSlotPreference { get; set; }
    public ICollection<Notification> Notifications { get; set; } = [];
}
```

---

### 3. Update `AppointmentConfiguration`

Edit `src/HealthPlatform.Infrastructure/Persistence/Configurations/AppointmentConfiguration.cs`.

Key changes:
- Mark `SlotId` as optional on the FK relationship.
- Change the `SlotId` unique index to a **filtered** unique index
  (`WHERE slot_id IS NOT NULL`) so multiple walk-in rows with `NULL` slot_id
  do not violate the unique constraint.
- Add `QueuePosition` and `ArrivalTime` column mappings.
- Add index on `(ProviderId, ArrivalTime)` to support fast daily-queue queries.

Full `Configure` method after changes:

```csharp
public void Configure(EntityTypeBuilder<Appointment> builder)
{
    builder.HasKey(a => a.Id);

    builder.Property(a => a.Status)
        .HasConversion<string>()
        .HasMaxLength(20);

    builder.Property(a => a.VisitReason).HasMaxLength(500);

    builder.HasIndex(a => a.PatientId);
    builder.HasIndex(a => a.ProviderId);

    // Filtered unique index: only non-null SlotId values must be unique.
    // NULL values (walk-ins) are excluded from the constraint.
    builder.HasIndex(a => a.SlotId)
        .IsUnique()
        .HasFilter("slot_id IS NOT NULL");

    builder.HasIndex(a => new { a.PatientId, a.ProviderId, a.SlotTime });

    // Index to support fast provider daily-queue queries.
    builder.HasIndex(a => new { a.ProviderId, a.ArrivalTime });

    builder.HasOne(a => a.Patient)
        .WithMany(p => p.Appointments)
        .HasForeignKey(a => a.PatientId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne(a => a.Provider)
        .WithMany(p => p.Appointments)
        .HasForeignKey(a => a.ProviderId)
        .OnDelete(DeleteBehavior.Restrict);

    // Optional relationship: walk-in appointments have no slot.
    builder.HasOne(a => a.Slot)
        .WithOne(s => s.Appointment)
        .HasForeignKey<Appointment>(a => a.SlotId)
        .IsRequired(false)
        .OnDelete(DeleteBehavior.Restrict);

    // PostgreSQL xmin system column as optimistic-concurrency token (Npgsql 8.x)
    builder.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
}
```

---

### 4. Generate EF Migration

```bash
cd src
dotnet ef migrations add AddWalkInAppointmentFields \
    --project HealthPlatform.Infrastructure \
    --startup-project HealthPlatform.Api \
    --output-dir Persistence/Migrations
```

The migration will:
- Alter `slot_id` column on `appointments` to be **nullable** (`character(36) NULL`).
- Add `queue_position integer NULL` column.
- Add `arrival_time timestamp with time zone NULL` column.
- Drop the existing unique index `ix_appointments_slot_id`.
- Create filtered unique index `ix_appointments_slot_id` with `WHERE slot_id IS NOT NULL`.
- Add index `ix_appointments_provider_id_arrival_time`.

> **Review the generated migration** before applying. Ensure the existing
> unique index is dropped and the filtered one is created in its place.

---

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Domain/Enums/AppointmentStatus.cs` | Add `WalkIn = 6` |
| `src/HealthPlatform.Domain/Entities/Appointment.cs` | `SlotId → Guid?`, `Slot → optional nav`, add `QueuePosition`, `ArrivalTime` |
| `src/HealthPlatform.Infrastructure/Persistence/Configurations/AppointmentConfiguration.cs` | Nullable FK, filtered unique index, new columns + index |
| `src/HealthPlatform.Infrastructure/Persistence/Migrations/YYYYMMDDHHMMSS_AddWalkInAppointmentFields.cs` | New |

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Release
dotnet test HealthPlatform.sln --no-build --configuration Release
```

Build succeeds. All 8 existing tests pass.
