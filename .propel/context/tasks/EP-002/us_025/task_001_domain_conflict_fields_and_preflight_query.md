# Task 001: Domain Fields + Conflict Specification + Pre-flight Query

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-025 |
| **Epic** | EP-002 |
| **Layer** | Domain (entity + migration) + Application (specification + CQRS query) |
| **Priority** | High |
| **Estimated Effort** | 60 minutes |
| **Dependencies** | None |

## Objective

Three related deliverables that form the foundation for all conflict detection:

1. **Domain** — add `IsConflictOverride` and `ConflictOverrideReason` fields to the
   `Appointment` entity so that force-bookings are visible in the auto-captured audit
   log (written by `AuditSaveChangesInterceptor`).  Requires an EF Core migration.

2. **`PatientActiveSameDayAppointmentsSpecification`** — returns all non-terminal
   appointments for a patient on a calendar day across **any** provider (unlike the
   existing `ActiveAppointmentByPatientProviderDateSpecification` which is scoped to
   one provider).  Eagerly loads `Provider` so handlers can surface provider names.

3. **`CheckAppointmentConflictsQuery`** + Handler — read-only pre-flight query that
   classifies conflicts as `"None"`, `"Soft"`, or `"Hard"` before any booking
   attempt.  UI callers use this to show warnings; the booking handler (Task 002)
   re-uses the same specification inline.

---

## Acceptance Criteria Covered

- AC: System detects overlap within 30 min before/after proposed slot (hard)
- AC: Soft conflict (same day, different time): produces warning, allows booking
- AC: Hard conflict: blocks booking, exposes conflicting appointment details
- AC: Conflict check occurs before slot lock (fail fast)
- AC: Audit log for conflict overrides *(interceptor captures entity fields)*

---

## Implementation Steps

### 1. Add override fields to `Appointment` entity

Edit `src/HealthPlatform.Domain/Entities/Appointment.cs`.

Add after `CancellationNote`:

```csharp
    public bool    IsConflictOverride     { get; set; }  // true when staff force-booked
    public string? ConflictOverrideReason { get; set; }  // required when IsConflictOverride
```

---

### 2. Configure new columns in `AppointmentConfiguration`

Edit `src/HealthPlatform.Infrastructure/Persistence/Configurations/AppointmentConfiguration.cs`.

Add after the `builder.Property(a => a.CancellationNote)` line:

```csharp
        builder.Property(a => a.ConflictOverrideReason).HasMaxLength(500);
        // IsConflictOverride is a non-nullable bool; EF maps it to boolean with default false.
```

---

### 3. Add EF Core migration

Run from repo root:

```bash
dotnet ef migrations add AddConflictOverrideFields `
    --project  src/HealthPlatform.Infrastructure `
    --startup-project src/HealthPlatform.Api
```

Expected generated migration (`src/HealthPlatform.Infrastructure/Persistence/Migrations/…_AddConflictOverrideFields.cs`):

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<bool>(
        name:      "is_conflict_override",
        table:     "appointments",
        nullable:  false,
        defaultValue: false);

    migrationBuilder.AddColumn<string>(
        name:      "conflict_override_reason",
        table:     "appointments",
        type:      "character varying(500)",
        maxLength: 500,
        nullable:  true);
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(name: "is_conflict_override",     table: "appointments");
    migrationBuilder.DropColumn(name: "conflict_override_reason", table: "appointments");
}
```

Apply:

```bash
dotnet ef database update `
    --project src/HealthPlatform.Infrastructure `
    --startup-project src/HealthPlatform.Api
```

---

### 4. `PatientActiveSameDayAppointmentsSpecification`

Create `src/HealthPlatform.Application/Features/Appointments/PatientActiveSameDayAppointmentsSpecification.cs`:

```csharp
using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Returns all non-terminal appointments for a patient on a given UTC calendar
/// day across ANY provider.  Used for cross-provider conflict detection.
///
/// Non-terminal statuses included: Scheduled, Booked, Arrived, InProgress.
/// Terminal statuses excluded:  Cancelled, NoShow, Completed.
///
/// Eagerly loads the Provider navigation so callers can surface provider names
/// without a second query.
///
/// The optional <paramref name="excludeAppointmentId"/> allows the rescheduling
/// flow to exclude the appointment being rescheduled (self-exclusion).
/// </summary>
internal sealed class PatientActiveSameDayAppointmentsSpecification : ISpecification<Appointment>
{
    private readonly Guid            _patientId;
    private readonly DateTimeOffset  _dayStart;
    private readonly DateTimeOffset  _dayEnd;
    private readonly Guid?           _excludeAppointmentId;

    public PatientActiveSameDayAppointmentsSpecification(
        Guid     patientId,
        DateOnly date,
        Guid?    excludeAppointmentId = null)
    {
        _patientId            = patientId;
        _excludeAppointmentId = excludeAppointmentId;
        _dayStart = new DateTimeOffset(date.Year, date.Month, date.Day,  0,  0,  0, TimeSpan.Zero);
        _dayEnd   = new DateTimeOffset(date.Year, date.Month, date.Day, 23, 59, 59, TimeSpan.Zero);
    }

    public Expression<Func<Appointment, bool>>? Criteria =>
        a => a.PatientId == _patientId
          && a.SlotTime  >= _dayStart
          && a.SlotTime  <= _dayEnd
          && (a.Status == AppointmentStatus.Scheduled
           || a.Status == AppointmentStatus.Booked
           || a.Status == AppointmentStatus.Arrived
           || a.Status == AppointmentStatus.InProgress)
          && (_excludeAppointmentId == null || a.Id != _excludeAppointmentId.Value);

    public List<Expression<Func<Appointment, object>>> Includes =>
    [
        a => a.Provider   // needed so handlers can return ConflictingProviderName
    ];

    public Expression<Func<Appointment, object>>?      OrderBy           => a => a.SlotTime;
    public Expression<Func<Appointment, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
```

---

### 5. `CheckAppointmentConflictsQuery` + DTOs

Create `src/HealthPlatform.Application/Features/Appointments/CheckAppointmentConflictsQuery.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Pre-flight read-only conflict check for a proposed slot booking.
/// Returns the worst conflict severity for the authenticated patient against the
/// requested slot time.
///
/// Severity values:
///   "None" — no conflicts; proceed with booking.
///   "Soft" — same day, different time (> 30-min gap); warning, booking allowed.
///   "Hard" — time window overlap (within 30 min); booking blocked for patients,
///             overridable by Staff with a reason.
/// </summary>
public sealed record CheckAppointmentConflictsQuery(
    Guid PatientId,
    Guid SlotId)
    : IRequest<ConflictCheckResultDto>;

public sealed record ConflictCheckResultDto(
    string         Severity,                    // "None" | "Soft" | "Hard"
    Guid?          ConflictingAppointmentId,
    string?        ConflictingProviderName,
    DateTimeOffset? ConflictingSlotTime,
    string?        Message);
```

---

### 6. `CheckAppointmentConflictsQueryHandler`

Create `src/HealthPlatform.Application/Features/Appointments/CheckAppointmentConflictsQueryHandler.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Handles <see cref="CheckAppointmentConflictsQuery"/>.
///
/// Flow:
///  1. Load the requested slot to get the proposed SlotTime.
///  2. Load all non-terminal appointments for the patient on that calendar day
///     (across all providers) via PatientActiveSameDayAppointmentsSpecification.
///  3. Classify: hard if |SlotTime delta| &lt; 30 min; soft otherwise.
///  4. Return the worst conflict found (hard > soft > none), with details of the
///     first conflicting appointment for UI display.
/// </summary>
internal sealed class CheckAppointmentConflictsQueryHandler
    : IRequestHandler<CheckAppointmentConflictsQuery, ConflictCheckResultDto>
{
    private const int HardConflictWindowMinutes = 30;

    private readonly IUnitOfWork _uow;

    public CheckAppointmentConflictsQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<ConflictCheckResultDto> Handle(
        CheckAppointmentConflictsQuery query,
        CancellationToken              ct)
    {
        // ── 1. Load slot ───────────────────────────────────────────────────
        var slot = await _uow.Repository<AppointmentSlot>()
            .GetByIdAsync(query.SlotId, ct)
            ?? throw new NotFoundException(nameof(AppointmentSlot), query.SlotId);

        var proposedTime = slot.StartTime;
        var proposedDate = DateOnly.FromDateTime(proposedTime.UtcDateTime);

        // ── 2. Load same-day active appointments ──────────────────────────
        var existing = await _uow.Repository<Appointment>()
            .GetAsync(
                new PatientActiveSameDayAppointmentsSpecification(query.PatientId, proposedDate),
                ct);

        if (existing.Count == 0)
            return new ConflictCheckResultDto("None", null, null, null, null);

        // ── 3. Classify ───────────────────────────────────────────────────
        // Hard: |delta| < 30 min
        var hardConflict = existing.FirstOrDefault(
            a => Math.Abs((a.SlotTime - proposedTime).TotalMinutes) < HardConflictWindowMinutes);

        if (hardConflict is not null)
            return new ConflictCheckResultDto(
                "Hard",
                hardConflict.Id,
                hardConflict.Provider.Name,
                hardConflict.SlotTime,
                $"You already have an appointment with {hardConflict.Provider.Name} at " +
                $"{hardConflict.SlotTime:t} UTC on the same day. " +
                "These appointments overlap within a 30-minute window.");

        // Soft: same day, outside hard window
        var softConflict = existing[0];
        return new ConflictCheckResultDto(
            "Soft",
            softConflict.Id,
            softConflict.Provider.Name,
            softConflict.SlotTime,
            $"You have another appointment with {softConflict.Provider.Name} at " +
            $"{softConflict.SlotTime:t} UTC on the same day. " +
            "You can still proceed with this booking.");
    }
}
```

---

## Verification

```bash
dotnet build src/HealthPlatform.sln
# Expected: 0 errors, 0 warnings
```

**Files created/updated:**
- `src/HealthPlatform.Domain/Entities/Appointment.cs` — updated
- `src/HealthPlatform.Infrastructure/Persistence/Configurations/AppointmentConfiguration.cs` — updated
- `src/HealthPlatform.Infrastructure/Persistence/Migrations/…_AddConflictOverrideFields.cs` — generated
- `src/HealthPlatform.Application/Features/Appointments/PatientActiveSameDayAppointmentsSpecification.cs` — new
- `src/HealthPlatform.Application/Features/Appointments/CheckAppointmentConflictsQuery.cs` — new
- `src/HealthPlatform.Application/Features/Appointments/CheckAppointmentConflictsQueryHandler.cs` — new
