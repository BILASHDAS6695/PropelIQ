# Task 001: Application Layer — CQRS (Search + Check-In + Revert)

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-023 |
| **Epic** | EP-002 |
| **Layer** | Application (CQRS queries, commands, specifications) |
| **Priority** | High |
| **Estimated Effort** | 90 minutes |
| **Dependencies** | No migration needed — `Appointment.ArrivalTime` (nullable `DateTimeOffset`) and `AppointmentStatus.Arrived = 2` already exist in the domain. `AppointmentByIdWithSlotAndPatientSpecification` (US-022) is available for reuse. |

## Objective

Implement three operations for the staff check-in workflow:

1. **Search** — query today's appointments by patient name fragment or exact appointment ID.
2. **Mark Arrived** — transition `Scheduled`/`Booked` → `Arrived`, stamp `ArrivalTime`, compute late-arrival flag.
3. **Revert Arrival** — undo a mistaken check-in within 5 minutes (transition back to `Scheduled`).

Additionally, expose arrival data in the provider's active queue by updating
`ProviderQueueByDateSpecification` to include `Arrived` appointments and enriching
`QueueEntryDto` with `ArrivalTime` and `IsLateArrival`.

All audit log entries are written automatically by `AuditSaveChangesInterceptor`.
The SignalR broadcast for provider-dashboard notifications is deferred to the API
controller (Task 002) to keep this layer infrastructure-free.

## Acceptance Criteria Covered

- AC: Staff can search today's appointments by patient name or appointment ID
- AC: Staff marks appointment status from "Scheduled" → "Arrived"
- AC: Arrival timestamp recorded automatically
- AC: Arrived patients appear in provider's active queue
- AC: If patient arrives > 15 min late, flag as "Late Arrival"
- AC: Audit log entry for arrival marking *(interceptor auto-captures)*
- Edge: Staff accidentally marks wrong patient → can undo within 5 minutes (revert to Scheduled)

---

## Implementation Steps

### 1. `TodayAppointmentsSearchQuery` + Validator + Specification + Handler

#### 1a. Query Record & DTO

Create `src/HealthPlatform.Application/Features/Appointments/TodayAppointmentsSearchQuery.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Returns today's appointments optionally scoped to one provider and filtered
/// by a patient name fragment (case-insensitive partial match) or an exact
/// appointment ID.  Intended for the front-desk staff check-in search screen.
/// </summary>
public sealed record TodayAppointmentsSearchQuery(
    Guid?   ProviderId,
    string? PatientNameFragment,
    Guid?   AppointmentId)
    : IRequest<IReadOnlyList<TodayAppointmentItemDto>>;

public sealed record TodayAppointmentItemDto(
    Guid            AppointmentId,
    Guid            PatientId,
    string          PatientFullName,
    string          Status,
    DateTimeOffset  SlotTime,
    bool            IsWalkIn,
    bool            IsLateArrival,
    DateTimeOffset? ArrivalTime);
```

#### 1b. Validator

Create `src/HealthPlatform.Application/Features/Appointments/TodayAppointmentsSearchQueryValidator.cs`:

```csharp
using FluentValidation;

namespace HealthPlatform.Application.Features.Appointments;

internal sealed class TodayAppointmentsSearchQueryValidator
    : AbstractValidator<TodayAppointmentsSearchQuery>
{
    public TodayAppointmentsSearchQueryValidator()
    {
        // At least one filter must be provided to avoid a full-table scan
        RuleFor(q => q)
            .Must(q => q.ProviderId.HasValue
                    || !string.IsNullOrWhiteSpace(q.PatientNameFragment)
                    || q.AppointmentId.HasValue)
            .WithMessage("At least one search filter (ProviderId, PatientNameFragment, or AppointmentId) is required.");

        When(q => !string.IsNullOrWhiteSpace(q.PatientNameFragment), () =>
        {
            RuleFor(q => q.PatientNameFragment)
                .MinimumLength(2)
                .WithMessage("Patient name search requires at least 2 characters.");
        });
    }
}
```

#### 1c. Specification

Create `src/HealthPlatform.Application/Features/Appointments/TodayAppointmentsSearchSpecification.cs`:

```csharp
using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Returns today's non-terminal appointments (excluding Cancelled and Completed)
/// with optional narrowing by provider, patient name fragment, or appointment ID.
/// Eagerly loads the Patient navigation for name display and name-based filtering.
/// </summary>
internal sealed class TodayAppointmentsSearchSpecification : ISpecification<Appointment>
{
    private readonly Guid?          _providerId;
    private readonly DateTimeOffset _dayStart;
    private readonly DateTimeOffset _dayEnd;
    private readonly string?        _nameFragment;   // pre-lowercased
    private readonly Guid?          _appointmentId;

    public TodayAppointmentsSearchSpecification(
        Guid?    providerId,
        DateOnly today,
        string?  nameFragment,
        Guid?    appointmentId)
    {
        _providerId    = providerId;
        _dayStart      = new DateTimeOffset(today.Year, today.Month, today.Day, 0,  0,  0, TimeSpan.Zero);
        _dayEnd        = new DateTimeOffset(today.Year, today.Month, today.Day, 23, 59, 59, TimeSpan.Zero);
        _nameFragment  = nameFragment?.Trim().ToLower();
        _appointmentId = appointmentId;
    }

    public Expression<Func<Appointment, bool>>? Criteria =>
        a => a.SlotTime >= _dayStart
          && a.SlotTime <= _dayEnd
          && a.Status != AppointmentStatus.Cancelled
          && a.Status != AppointmentStatus.Completed
          && (_providerId    == null || a.ProviderId == _providerId)
          && (_appointmentId == null || a.Id         == _appointmentId)
          && (_nameFragment  == null
              || a.Patient.FirstName.ToLower().Contains(_nameFragment)
              || a.Patient.LastName.ToLower().Contains(_nameFragment));

    public List<Expression<Func<Appointment, object>>> Includes =>
    [
        a => a.Patient
    ];

    public Expression<Func<Appointment, object>>?      OrderBy           => a => a.SlotTime;
    public Expression<Func<Appointment, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
```

#### 1d. Handler

Create `src/HealthPlatform.Application/Features/Appointments/TodayAppointmentsSearchQueryHandler.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

internal sealed class TodayAppointmentsSearchQueryHandler
    : IRequestHandler<TodayAppointmentsSearchQuery, IReadOnlyList<TodayAppointmentItemDto>>
{
    private readonly IUnitOfWork _uow;

    public TodayAppointmentsSearchQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<TodayAppointmentItemDto>> Handle(
        TodayAppointmentsSearchQuery query,
        CancellationToken            ct)
    {
        var today         = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var appointments  = await _uow.Repository<Appointment>()
            .GetAsync(
                new TodayAppointmentsSearchSpecification(
                    query.ProviderId,
                    today,
                    query.PatientNameFragment,
                    query.AppointmentId),
                ct);

        return appointments
            .Select(a => new TodayAppointmentItemDto(
                a.Id,
                a.PatientId,
                $"{a.Patient.FirstName} {a.Patient.LastName}",
                a.Status.ToString(),
                a.SlotTime,
                a.IsWalkIn,
                IsLate(a),
                a.ArrivalTime))
            .ToList();
    }

    private static bool IsLate(Appointment a) =>
        a.ArrivalTime.HasValue && a.ArrivalTime.Value > a.SlotTime.AddMinutes(15);
}
```

---

### 2. `MarkPatientArrivedCommand` + Validator + Handler

#### 2a. Command & DTO

Create `src/HealthPlatform.Application/Features/Appointments/MarkPatientArrivedCommand.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Marks a booked appointment as Arrived and stamps the check-in timestamp.
/// Staff/Admin only — enforced at the controller level via [Authorize(Policy = PolicyNames.Staff)].
/// </summary>
public sealed record MarkPatientArrivedCommand(Guid AppointmentId)
    : IRequest<ArrivalConfirmationDto>;

/// <summary>
/// Returned to the API controller so it can broadcast a SignalR notification
/// to the provider's dashboard group without the handler depending on SignalR.
/// </summary>
public sealed record ArrivalConfirmationDto(
    Guid           AppointmentId,
    Guid           ProviderId,
    Guid           PatientId,
    string         PatientFullName,
    DateTimeOffset ArrivalTime,
    bool           IsLateArrival);
```

#### 2b. Validator

Create `src/HealthPlatform.Application/Features/Appointments/MarkPatientArrivedCommandValidator.cs`:

```csharp
using FluentValidation;

namespace HealthPlatform.Application.Features.Appointments;

internal sealed class MarkPatientArrivedCommandValidator
    : AbstractValidator<MarkPatientArrivedCommand>
{
    public MarkPatientArrivedCommandValidator()
    {
        RuleFor(c => c.AppointmentId).NotEmpty();
    }
}
```

#### 2c. Handler

Create `src/HealthPlatform.Application/Features/Appointments/MarkPatientArrivedCommandHandler.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Handles <see cref="MarkPatientArrivedCommand"/>.
///
/// Flow:
///  1. Load appointment with Patient navigation via specification.
///  2. Guard: only Scheduled or Booked appointments may transition to Arrived.
///  3. Mutate: Status = Arrived, ArrivalTime = UtcNow.
///  4. SaveChanges — AuditSaveChangesInterceptor writes the audit log entry.
///  5. Return DTO with ProviderId and IsLateArrival so the controller can
///     broadcast a SignalR notification to the provider's dashboard group.
/// </summary>
internal sealed class MarkPatientArrivedCommandHandler
    : IRequestHandler<MarkPatientArrivedCommand, ArrivalConfirmationDto>
{
    private readonly IUnitOfWork _uow;

    public MarkPatientArrivedCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<ArrivalConfirmationDto> Handle(
        MarkPatientArrivedCommand command,
        CancellationToken         ct)
    {
        // ── 1. Load appointment with Patient ──────────────────────────────
        var results = await _uow.Repository<Appointment>()
            .GetAsync(new AppointmentByIdWithSlotAndPatientSpecification(command.AppointmentId), ct);

        if (results.Count == 0)
            throw new NotFoundException(nameof(Appointment), command.AppointmentId);

        var appointment = results[0];

        // ── 2. Status guard ───────────────────────────────────────────────
        if (appointment.Status is not (AppointmentStatus.Scheduled or AppointmentStatus.Booked))
            throw new ArgumentException(
                $"Cannot check in an appointment with status '{appointment.Status}'. " +
                "Only Scheduled or Booked appointments can be marked as Arrived.");

        // ── 3. Mutate ─────────────────────────────────────────────────────
        var arrivedAt = DateTimeOffset.UtcNow;
        appointment.Status      = AppointmentStatus.Arrived;
        appointment.ArrivalTime = arrivedAt;
        _uow.Repository<Appointment>().Update(appointment);

        // ── 4. Persist (interceptor auto-writes audit log) ────────────────
        await _uow.SaveChangesAsync(ct);

        // ── 5. Compute late flag (SlotTime + 15 min) ──────────────────────
        bool isLate = arrivedAt > appointment.SlotTime.AddMinutes(15);

        return new ArrivalConfirmationDto(
            appointment.Id,
            appointment.ProviderId,
            appointment.PatientId,
            $"{appointment.Patient.FirstName} {appointment.Patient.LastName}",
            arrivedAt,
            isLate);
    }
}
```

---

### 3. `RevertArrivalCommand` + Validator + Handler

#### 3a. Command & DTO

Create `src/HealthPlatform.Application/Features/Appointments/RevertArrivalCommand.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Reverts an accidental check-in back to Scheduled status.
/// Only allowed within 5 minutes of the original check-in timestamp.
/// Staff/Admin only — enforced at the controller level.
/// </summary>
public sealed record RevertArrivalCommand(Guid AppointmentId)
    : IRequest<RevertArrivalConfirmationDto>;

public sealed record RevertArrivalConfirmationDto(
    Guid   AppointmentId,
    string Status,
    string Message);
```

#### 3b. Validator

Create `src/HealthPlatform.Application/Features/Appointments/RevertArrivalCommandValidator.cs`:

```csharp
using FluentValidation;

namespace HealthPlatform.Application.Features.Appointments;

internal sealed class RevertArrivalCommandValidator
    : AbstractValidator<RevertArrivalCommand>
{
    public RevertArrivalCommandValidator()
    {
        RuleFor(c => c.AppointmentId).NotEmpty();
    }
}
```

#### 3c. Handler

Create `src/HealthPlatform.Application/Features/Appointments/RevertArrivalCommandHandler.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Handles <see cref="RevertArrivalCommand"/>.
///
/// Flow:
///  1. Load appointment by ID (no navigation includes needed).
///  2. Guard: Status must be Arrived (cannot revert other statuses).
///  3. Guard: ArrivalTime must be within the last 5 minutes.
///  4. Mutate: Status = Scheduled, ArrivalTime = null.
///  5. SaveChanges — AuditSaveChangesInterceptor writes the audit log entry.
/// </summary>
internal sealed class RevertArrivalCommandHandler
    : IRequestHandler<RevertArrivalCommand, RevertArrivalConfirmationDto>
{
    private readonly IUnitOfWork _uow;

    public RevertArrivalCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<RevertArrivalConfirmationDto> Handle(
        RevertArrivalCommand command,
        CancellationToken    ct)
    {
        // ── 1. Load appointment ───────────────────────────────────────────
        var appointment = await _uow.Repository<Appointment>()
            .GetByIdAsync(command.AppointmentId, ct)
            ?? throw new NotFoundException(nameof(Appointment), command.AppointmentId);

        // ── 2. Status guard ───────────────────────────────────────────────
        if (appointment.Status != AppointmentStatus.Arrived)
            throw new ArgumentException(
                $"Cannot revert check-in: appointment status is '{appointment.Status}', not Arrived.");

        // ── 3. Time window guard (5 minutes) ──────────────────────────────
        var minutesSinceArrival = (DateTimeOffset.UtcNow - appointment.ArrivalTime!.Value).TotalMinutes;
        if (minutesSinceArrival > 5)
            throw new ArgumentException(
                "Cannot revert check-in: the 5-minute correction window has expired. " +
                "Contact a supervisor to manually adjust the appointment status.");

        // ── 4. Mutate ─────────────────────────────────────────────────────
        appointment.Status      = AppointmentStatus.Scheduled;
        appointment.ArrivalTime = null;
        _uow.Repository<Appointment>().Update(appointment);

        // ── 5. Persist (interceptor auto-writes audit log) ────────────────
        await _uow.SaveChangesAsync(ct);

        return new RevertArrivalConfirmationDto(
            appointment.Id,
            appointment.Status.ToString(),
            "Check-in reverted successfully. Appointment is now Scheduled.");
    }
}
```

---

### 4. Update `ProviderQueueByDateSpecification` — Add `Arrived` Status

Edit `src/HealthPlatform.Application/Features/Providers/ProviderQueueByDateSpecification.cs`.

Change the `Criteria` expression to include `AppointmentStatus.Arrived`:

```csharp
    public Expression<Func<Appointment, bool>>? Criteria =>
        a => a.ProviderId == _providerId
          && (a.Status == AppointmentStatus.Scheduled
           || a.Status == AppointmentStatus.WalkIn
           || a.Status == AppointmentStatus.Booked
           || a.Status == AppointmentStatus.Arrived)   // ← add this line
          && a.SlotTime >= _dayStart
          && a.SlotTime <= _dayEnd;
```

---

### 5. Enrich `QueueEntryDto` and its Handler with Arrival Data

Edit `src/HealthPlatform.Application/Features/Providers/GetProviderQueueQuery.cs`.

Add two new fields to the record so the provider's dashboard can display the
late-arrival indicator without a second call:

```csharp
public sealed record QueueEntryDto(
    Guid           AppointmentId,
    Guid           PatientId,
    string         Status,
    DateTimeOffset AppointmentTime,
    int?           QueuePosition,
    string?        VisitReason,
    bool           IsWalkIn,
    DateTimeOffset? ArrivalTime,    // ← new
    bool            IsLateArrival); // ← new
```

Edit `src/HealthPlatform.Application/Features/Providers/GetProviderQueueQueryHandler.cs`.

Update the projection to populate the two new fields:

```csharp
        return appointments
            .Select(a => new QueueEntryDto(
                a.Id,
                a.PatientId,
                a.Status.ToString(),
                a.IsWalkIn ? (a.ArrivalTime ?? a.SlotTime) : a.SlotTime,
                a.QueuePosition,
                a.VisitReason,
                a.IsWalkIn,
                a.ArrivalTime,
                a.ArrivalTime.HasValue && a.ArrivalTime.Value > a.SlotTime.AddMinutes(15)))
            .ToList();
```

---

## Verification

```bash
dotnet build src/HealthPlatform.sln
# Expected: 0 errors, 0 warnings
```

10 new files created, 3 existing files updated. No database migration required.
