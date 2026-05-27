# Task 002: Application Layer — Queue Dashboard + Status Transition

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-024 |
| **Epic** | EP-002 |
| **Layer** | Application (CQRS commands + queries, specifications) |
| **Priority** | High |
| **Estimated Effort** | 90 minutes |
| **Dependencies** | Task 001 (`AppointmentStatus.InProgress` available) |

## Objective

Two new operations plus enhancements to the existing queue query:

1. **`UpdateAppointmentStatusCommand`** — provider/staff advances an appointment
   through the `Arrived → InProgress → Completed` chain with guard validation.
   Returns `ProviderId` so the API controller can broadcast a SignalR event.
2. **`GetProviderQueueDashboardQuery`** — returns the full enriched queue for a
   provider/day with multi-key sort and a summary count block
   ("3 waiting, 1 in progress, 8 remaining").

Additionally update the existing `ProviderQueueByDateSpecification` (add `InProgress`
to the active filter + eager-load `Patient` for name) and `QueueEntryDto` (add
`PatientFullName`).  The existing `GetProviderQueueQueryHandler` is updated to
populate the new field.

Audit log entries are written automatically by `AuditSaveChangesInterceptor`.

## Acceptance Criteria Covered

- AC: Dashboard shows all appointments for selected date (default: today)
- AC: Each entry shows patient name, time, status, visit reason
- AC: Entries sorted: Arrived first (arrival time), then Scheduled (slot time), then Walk-ins (queue position)
- AC: Provider can change appointment status: Arrived → InProgress → Completed
- AC: Queue count summary: "N waiting, N in progress, N remaining"
- AC: Audit log entry *(interceptor auto-captures)*

---

## Implementation Steps

### 1. Update `ProviderQueueByDateSpecification`

Edit `src/HealthPlatform.Application/Features/Providers/ProviderQueueByDateSpecification.cs`.

Add `InProgress` to the status filter and eager-load `Patient` so handlers
can map `PatientFullName` without a second query:

```csharp
    public Expression<Func<Appointment, bool>>? Criteria =>
        a => a.ProviderId == _providerId
          && (a.Status == AppointmentStatus.Scheduled
           || a.Status == AppointmentStatus.WalkIn
           || a.Status == AppointmentStatus.Booked
           || a.Status == AppointmentStatus.Arrived
           || a.Status == AppointmentStatus.InProgress)  // ← add
          && a.SlotTime >= _dayStart
          && a.SlotTime <= _dayEnd;

    public List<Expression<Func<Appointment, object>>> Includes =>
    [
        a => a.Patient    // ← add; needed for PatientFullName mapping
    ];
```

---

### 2. Enrich `QueueEntryDto` with `PatientFullName`

Edit `src/HealthPlatform.Application/Features/Providers/GetProviderQueueQuery.cs`.

Add `PatientFullName` as the third field:

```csharp
public sealed record QueueEntryDto(
    Guid            AppointmentId,
    Guid            PatientId,
    string          PatientFullName,   // ← new
    string          Status,
    DateTimeOffset  AppointmentTime,
    int?            QueuePosition,
    string?         VisitReason,
    bool            IsWalkIn,
    DateTimeOffset? ArrivalTime,
    bool            IsLateArrival);
```

---

### 3. Update `GetProviderQueueQueryHandler` to populate `PatientFullName`

Edit `src/HealthPlatform.Application/Features/Providers/GetProviderQueueQueryHandler.cs`.

Insert the name field in the constructor call:

```csharp
        return appointments
            .Select(a => new QueueEntryDto(
                a.Id,
                a.PatientId,
                $"{a.Patient.FirstName} {a.Patient.LastName}",
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

### 4. `UpdateAppointmentStatusCommand` + Validator + Handler

#### 4a. Command & DTO

Create `src/HealthPlatform.Application/Features/Appointments/UpdateAppointmentStatusCommand.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Advances an appointment through the provider-driven status chain:
///   Arrived → InProgress → Completed
/// Staff/Admin only — enforced at the controller level.
/// Returns ProviderId so the API controller can broadcast a SignalR notification.
/// </summary>
public sealed record UpdateAppointmentStatusCommand(
    Guid   AppointmentId,
    string NewStatus)
    : IRequest<StatusUpdateConfirmationDto>;

public sealed record StatusUpdateConfirmationDto(
    Guid   AppointmentId,
    Guid   ProviderId,
    string OldStatus,
    string NewStatus);
```

#### 4b. Validator

Create `src/HealthPlatform.Application/Features/Appointments/UpdateAppointmentStatusCommandValidator.cs`:

```csharp
using FluentValidation;

namespace HealthPlatform.Application.Features.Appointments;

internal sealed class UpdateAppointmentStatusCommandValidator
    : AbstractValidator<UpdateAppointmentStatusCommand>
{
    private static readonly string[] AllowedTargetStatuses = ["InProgress", "Completed"];

    public UpdateAppointmentStatusCommandValidator()
    {
        RuleFor(c => c.AppointmentId).NotEmpty();

        RuleFor(c => c.NewStatus)
            .NotEmpty()
            .Must(s => AllowedTargetStatuses.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage(
                $"Allowed target statuses are: {string.Join(", ", AllowedTargetStatuses)}.");
    }
}
```

#### 4c. Handler

Create `src/HealthPlatform.Application/Features/Appointments/UpdateAppointmentStatusCommandHandler.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Handles <see cref="UpdateAppointmentStatusCommand"/>.
///
/// Flow:
///  1. Load appointment by ID.
///  2. Parse the requested target status.
///  3. Guard: verify the transition is in the allowed chain.
///  4. Mutate status.
///  5. SaveChanges — AuditSaveChangesInterceptor writes the audit log entry.
///  6. Return DTO with ProviderId so the controller can broadcast SignalR.
/// </summary>
internal sealed class UpdateAppointmentStatusCommandHandler
    : IRequestHandler<UpdateAppointmentStatusCommand, StatusUpdateConfirmationDto>
{
    /// <summary>
    /// Defines the only allowed forward transitions for provider-driven status updates.
    /// </summary>
    private static readonly Dictionary<AppointmentStatus, AppointmentStatus> AllowedTransitions =
        new()
        {
            [AppointmentStatus.Arrived]    = AppointmentStatus.InProgress,
            [AppointmentStatus.InProgress] = AppointmentStatus.Completed,
        };

    private readonly IUnitOfWork _uow;

    public UpdateAppointmentStatusCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<StatusUpdateConfirmationDto> Handle(
        UpdateAppointmentStatusCommand command,
        CancellationToken              ct)
    {
        // ── 1. Load appointment ───────────────────────────────────────────
        var appointment = await _uow.Repository<Appointment>()
            .GetByIdAsync(command.AppointmentId, ct)
            ?? throw new NotFoundException(nameof(Appointment), command.AppointmentId);

        // ── 2. Parse target status ────────────────────────────────────────
        if (!Enum.TryParse<AppointmentStatus>(command.NewStatus, ignoreCase: true, out var target))
            throw new ArgumentException(
                $"'{command.NewStatus}' is not a recognised appointment status.");

        // ── 3. Transition guard ───────────────────────────────────────────
        if (!AllowedTransitions.TryGetValue(appointment.Status, out var allowedNext)
            || allowedNext != target)
        {
            throw new ArgumentException(
                $"Cannot transition from '{appointment.Status}' to '{target}'. " +
                "Allowed transitions: Arrived → InProgress, InProgress → Completed.");
        }

        // ── 4. Mutate ─────────────────────────────────────────────────────
        var oldStatus = appointment.Status.ToString();
        appointment.Status = target;
        _uow.Repository<Appointment>().Update(appointment);

        // ── 5. Persist (interceptor auto-writes audit log) ────────────────
        await _uow.SaveChangesAsync(ct);

        return new StatusUpdateConfirmationDto(
            appointment.Id,
            appointment.ProviderId,
            oldStatus,
            appointment.Status.ToString());
    }
}
```

---

### 5. `GetProviderQueueDashboardQuery` + Handler

#### 5a. Query & DTOs

Create `src/HealthPlatform.Application/Features/Providers/GetProviderQueueDashboardQuery.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Providers;

/// <summary>
/// Returns the full daily queue for a provider with multi-key sort and a
/// summary count block suitable for the dashboard header.
///
/// Sort order (all in-memory after DB retrieval):
///   1. InProgress — currently being seen (by ArrivalTime ASC)
///   2. Arrived    — checked in, waiting (by ArrivalTime ASC)
///   3. Scheduled / Booked — upcoming (by SlotTime ASC)
///   4. WalkIn     — unscheduled (by QueuePosition ASC)
/// </summary>
public sealed record GetProviderQueueDashboardQuery(Guid ProviderId, DateOnly Date)
    : IRequest<QueueDashboardDto>;

public sealed record QueueDashboardDto(
    IReadOnlyList<QueueEntryDto> Items,
    QueueSummaryDto              Summary);

/// <summary>
/// Counts for the dashboard header: "N waiting, N in progress, N remaining".
/// </summary>
public sealed record QueueSummaryDto(
    int Waiting,     // Arrived — checked in, not yet InProgress
    int InProgress,  // InProgress — currently being seen
    int Remaining);  // Scheduled + Booked + WalkIn — not yet arrived
```

#### 5b. Handler

Create `src/HealthPlatform.Application/Features/Providers/GetProviderQueueDashboardQueryHandler.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Providers;

internal sealed class GetProviderQueueDashboardQueryHandler
    : IRequestHandler<GetProviderQueueDashboardQuery, QueueDashboardDto>
{
    private readonly IUnitOfWork _uow;

    public GetProviderQueueDashboardQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<QueueDashboardDto> Handle(
        GetProviderQueueDashboardQuery query,
        CancellationToken              ct)
    {
        var appointments = await _uow.Repository<Appointment>()
            .GetAsync(new ProviderQueueByDateSpecification(query.ProviderId, query.Date), ct);

        // ── Multi-key sort (in-memory) ────────────────────────────────────
        // Priority group: InProgress(0) > Arrived(1) > Scheduled/Booked(2) > WalkIn(3)
        var sorted = appointments
            .OrderBy(a => a.Status switch
            {
                AppointmentStatus.InProgress => 0,
                AppointmentStatus.Arrived    => 1,
                AppointmentStatus.WalkIn     => 3,
                _                            => 2   // Scheduled, Booked
            })
            .ThenBy(a => a.Status is AppointmentStatus.InProgress or AppointmentStatus.Arrived
                ? a.ArrivalTime ?? DateTimeOffset.MaxValue
                : a.Status == AppointmentStatus.WalkIn
                    ? DateTimeOffset.MinValue.AddMinutes(a.QueuePosition ?? int.MaxValue)
                    : a.SlotTime)
            .ToList();

        var items = sorted
            .Select(a => new QueueEntryDto(
                a.Id,
                a.PatientId,
                $"{a.Patient.FirstName} {a.Patient.LastName}",
                a.Status.ToString(),
                a.IsWalkIn ? (a.ArrivalTime ?? a.SlotTime) : a.SlotTime,
                a.QueuePosition,
                a.VisitReason,
                a.IsWalkIn,
                a.ArrivalTime,
                a.ArrivalTime.HasValue && a.ArrivalTime.Value > a.SlotTime.AddMinutes(15)))
            .ToList();

        var summary = new QueueSummaryDto(
            Waiting:    appointments.Count(a => a.Status == AppointmentStatus.Arrived),
            InProgress: appointments.Count(a => a.Status == AppointmentStatus.InProgress),
            Remaining:  appointments.Count(a =>
                a.Status is AppointmentStatus.Scheduled
                         or AppointmentStatus.Booked
                         or AppointmentStatus.WalkIn));

        return new QueueDashboardDto(items, summary);
    }
}
```

---

## Verification

```bash
dotnet build src/HealthPlatform.sln
# Expected: 0 errors, 0 warnings
```

5 new files created, 3 existing files updated.  No database migration required.
