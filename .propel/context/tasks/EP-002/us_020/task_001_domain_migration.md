# Task 001: Domain Changes + EF Configuration + Migration

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-020 |
| **Epic** | EP-002 |
| **Layer** | Domain + Infrastructure (EF config + migration) |
| **Priority** | Critical |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | US-019 Task 001 (SlotStatus + AppointmentSlot.Status must exist) |

## Objective

Extend the domain model to support the patient booking flow: add a `Scheduled`
status to `AppointmentStatus`, add a `VisitReason` field to `Appointment`, and
wire optimistic concurrency on `AppointmentSlot` so that the "first wins"
concurrent booking race is caught at the EF level.

## Acceptance Criteria Covered

- AC: On booking → Appointment created with status "Scheduled"
- AC: Patient provides visit reason (free text, max 500 chars)
- AC: Slot locked during booking (optimistic concurrency with version check)

---

## Implementation Steps

### 1. Add `Scheduled` to `AppointmentStatus` Enum

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
    NoShow    = 5
}
```

> **Note**: Existing `Booked` variant is kept for backward compatibility with
> already-stored string values. `Scheduled` is the new initial state used by
> the online booking flow.

---

### 2. Add `VisitReason` to `Appointment`

Edit `src/HealthPlatform.Domain/Entities/Appointment.cs` — add the property:

```csharp
public string? VisitReason { get; set; }
```

Place it after the `IsWalkIn` property:

```csharp
public bool    IsWalkIn    { get; set; }
public string? VisitReason { get; set; }
```

---

### 3. Update `AppointmentConfiguration`

Edit `src/HealthPlatform.Infrastructure/Persistence/Configurations/AppointmentConfiguration.cs`:

**Add `VisitReason` column constraint:**
```csharp
builder.Property(a => a.VisitReason).HasMaxLength(500);
```

**Add composite index for "one active appointment per provider per day" lookup
(checked in the handler; index makes the query fast):**
```csharp
builder.HasIndex(a => new { a.PatientId, a.ProviderId, a.SlotTime });
```

The full `Configure` method after changes:
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
    builder.HasIndex(a => a.SlotId).IsUnique();
    builder.HasIndex(a => new { a.PatientId, a.ProviderId, a.SlotTime });

    builder.HasOne(a => a.Patient)
        .WithMany(p => p.Appointments)
        .HasForeignKey(a => a.PatientId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne(a => a.Provider)
        .WithMany(p => p.Appointments)
        .HasForeignKey(a => a.ProviderId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne(a => a.Slot)
        .WithOne(s => s.Appointment)
        .HasForeignKey<Appointment>(a => a.SlotId)
        .OnDelete(DeleteBehavior.Restrict);

    // PostgreSQL xmin on Appointment (already present — do not remove)
    builder.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
}
```

---

### 4. Add `xmin` Row Version to `AppointmentSlotConfiguration`

Edit `src/HealthPlatform.Infrastructure/Persistence/Configurations/AppointmentSlotConfiguration.cs`
— add after the existing `HasIndex` calls:

```csharp
// PostgreSQL xmin system column as optimistic-concurrency token.
// Allows EF to detect concurrent slot-status changes and throw
// DbUpdateConcurrencyException ("first wins" booking race).
builder.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
```

> **Note**: `xmin` is a PostgreSQL built-in system column — no migration column
> addition is needed. EF maps it as a shadow property.

---

### 5. Generate EF Migration

```bash
cd src
dotnet ef migrations add AddAppointmentBookingFields \
    --project HealthPlatform.Infrastructure \
    --startup-project HealthPlatform.Api \
    --output-dir Persistence/Migrations
```

The migration will:
- Add `visit_reason character varying(500)` nullable column to `appointments`
- Add composite index `ix_appointments_patient_id_provider_id_slot_time`
- No changes to `appointment_slots` (xmin is a system column, not in migration)

---

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Domain/Enums/AppointmentStatus.cs` | Add `Scheduled = 0` |
| `src/HealthPlatform.Domain/Entities/Appointment.cs` | Add `VisitReason` property |
| `src/HealthPlatform.Infrastructure/Persistence/Configurations/AppointmentConfiguration.cs` | Add `VisitReason` max-length + composite index |
| `src/HealthPlatform.Infrastructure/Persistence/Configurations/AppointmentSlotConfiguration.cs` | Add `xmin` row version |
| `src/HealthPlatform.Infrastructure/Persistence/Migrations/YYYYMMDDHHMMSS_AddAppointmentBookingFields.cs` | New |

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Release
dotnet test HealthPlatform.sln --no-build --configuration Release
```

No compile errors. All 6 existing tests pass.
