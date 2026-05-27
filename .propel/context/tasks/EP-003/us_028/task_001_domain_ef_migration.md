# Task 001: Domain + EF Configuration + Migration

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-028 |
| **Epic** | EP-003 |
| **Layer** | Domain + Infrastructure (EF config + migration) |
| **Priority** | Medium |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | US-021 Task 001 (`Appointment` with nullable `SlotId`), US-020 Task 001 (`AppointmentStatus.Scheduled` exists) |

## Objective

Introduce the `SlotSwapRequest` aggregate and its backing `SlotSwapStatus` enum.
Every patient-initiated swap request is represented as a first-class entity with
a 24-hour expiry, privacy-preserving references, and a one-active-request-per-appointment
constraint enforced by a filtered unique index.

## Acceptance Criteria Covered

- AC: Swap request created with status "Pending" containing: requester, target slot, offered slot
- AC: Only one active swap request per appointment allowed
- AC: Swap request expires after 24 hours if no response
- AC: Audit log entry for swap request creation (auto-stamped by `AuditSaveChangesInterceptor`)

---

## Implementation Steps

### 1. Add `SlotSwapStatus` Enum

Create `src/HealthPlatform.Domain/Enums/SlotSwapStatus.cs`:

```csharp
namespace HealthPlatform.Domain.Enums;

public enum SlotSwapStatus
{
    Pending   = 0,   // Awaiting target patient response
    Accepted  = 1,   // Target patient accepted (handled by US-029)
    Declined  = 2,   // Target patient declined (handled by US-029)
    Cancelled = 3,   // Requester cancelled the request
    Expired   = 4    // No response within 24 hours
}
```

---

### 2. Create `SlotSwapRequest` Entity

Create `src/HealthPlatform.Domain/Entities/SlotSwapRequest.cs`:

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

    // ── Navigation properties ──────────────────────────────────────────────
    public PatientProfile     RequesterPatient     { get; set; } = null!;
    public Appointment        RequesterAppointment { get; set; } = null!;
    public Appointment        TargetAppointment    { get; set; } = null!;
}
```

---

### 3. Add Navigation Collection to `Appointment`

Edit `src/HealthPlatform.Domain/Entities/Appointment.cs` — append two collections
inside the class body (after `PreferredSlotPreference`):

```csharp
    // Swap requests initiated BY this appointment (requester side)
    public ICollection<SlotSwapRequest> InitiatedSwapRequests  { get; set; } = [];
    // Swap requests targeting THIS appointment (target side)
    public ICollection<SlotSwapRequest> ReceivedSwapRequests   { get; set; } = [];
```

---

### 4. Create `SlotSwapRequestConfiguration`

Create `src/HealthPlatform.Infrastructure/Persistence/Configurations/SlotSwapRequestConfiguration.cs`:

```csharp
using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class SlotSwapRequestConfiguration
    : IEntityTypeConfiguration<SlotSwapRequest>
{
    public void Configure(EntityTypeBuilder<SlotSwapRequest> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(r => r.CancellationReason)
            .HasMaxLength(500);

        // ── Filtered unique index: only one active (Pending) swap request ──
        // per requester appointment. Completed/cancelled/expired requests are
        // excluded so historical records can accumulate.
        builder.HasIndex(r => r.RequesterAppointmentId)
            .IsUnique()
            .HasFilter("status = 'Pending'")
            .HasDatabaseName("ix_slot_swap_requests_active_per_appointment");

        // ── Support fast expiry-sweep queries ─────────────────────────────
        builder.HasIndex(r => new { r.Status, r.ExpiresAt })
            .HasDatabaseName("ix_slot_swap_requests_status_expires");

        // ── Relationships ─────────────────────────────────────────────────
        builder.HasOne(r => r.RequesterPatient)
            .WithMany()
            .HasForeignKey(r => r.RequesterPatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.RequesterAppointment)
            .WithMany(a => a.InitiatedSwapRequests)
            .HasForeignKey(r => r.RequesterAppointmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.TargetAppointment)
            .WithMany(a => a.ReceivedSwapRequests)
            .HasForeignKey(r => r.TargetAppointmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

---

### 5. Register `SlotSwapRequest` in `ApplicationDbContext`

Edit `src/HealthPlatform.Infrastructure/Persistence/ApplicationDbContext.cs` —
add one `DbSet` property alongside the existing sets:

```csharp
public DbSet<SlotSwapRequest> SlotSwapRequests => Set<SlotSwapRequest>();
```

---

### 6. Generate EF Core Migration

Run from the repository root (PowerShell):

```powershell
& "C:\Program Files\dotnet\dotnet.exe" ef migrations add AddSlotSwapRequest `
    --project "src\HealthPlatform.Infrastructure" `
    --startup-project "src\HealthPlatform.Api" `
    --output-dir "Persistence\Migrations"
```

Verify the generated migration contains:

- `CreateTable("slot_swap_requests", ...)` with all columns (snake_case).
- `CreateIndex("ix_slot_swap_requests_active_per_appointment", ...)` with `filter: "status = 'Pending'"`.
- `CreateIndex("ix_slot_swap_requests_status_expires", ...)`.
- Three `AddForeignKey` calls (requester_patient_id, requester_appointment_id, target_appointment_id).

Apply the migration:

```powershell
& "C:\Program Files\dotnet\dotnet.exe" ef database update `
    --project "src\HealthPlatform.Infrastructure" `
    --startup-project "src\HealthPlatform.Api" `
    --connection "Host=localhost;Port=5432;Database=healthplatform;Username=postgres;Password=admin123;Maximum Pool Size=100"
```

---

## Definition of Done

- [ ] `SlotSwapStatus.cs` created in `Domain/Enums/`
- [ ] `SlotSwapRequest.cs` created in `Domain/Entities/` with all properties and navigation properties
- [ ] `Appointment` entity has `InitiatedSwapRequests` and `ReceivedSwapRequests` collections
- [ ] `SlotSwapRequestConfiguration.cs` created with filtered unique index on `(RequesterAppointmentId, status='Pending')`
- [ ] `ApplicationDbContext.SlotSwapRequests` DbSet registered
- [ ] Migration `AddSlotSwapRequest` generated and applied without errors
- [ ] `dotnet build` succeeds with no errors
