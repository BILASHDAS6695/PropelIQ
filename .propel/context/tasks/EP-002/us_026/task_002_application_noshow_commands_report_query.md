# Task 002: Application Layer — MarkNoShow Command + NoShow Report Query + NoShow→Arrived Override

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-026 |
| **Epic** | EP-002 |
| **Layer** | Application (CQRS commands + specifications + query) |
| **Priority** | High |
| **Estimated Effort** | 90 minutes |
| **Dependencies** | Task 001 (TotalNoShowCount on PatientProfile, Hangfire registered) |

## Objective

Five deliverables that cover the core business logic for no-show tracking:

1. **`ActiveUnattendedPastCutoffSpecification`** — fetches Scheduled/Booked
   appointments where `SlotTime + 30 min <= cutoffTime` and `ArrivalTime` is
   null, eagerly loading `Slot` and `Patient`.  Used by the Hangfire auto-mark
   job (Task 003).

2. **`MarkNoShowCommand`** + validator + handler — marks a single appointment
   as `NoShow`, frees the slot, increments `TotalNoShowCount`, sends the
   follow-up email, and returns the `ProviderId` so the API controller can
   broadcast a SignalR event.

3. **`GetNoShowReportQuery`** + handler — returns no-show stats aggregated by
   provider, day of week, and time slot for a given date range.  Admin only.

4. **`UpdateAppointmentStatusCommandValidator`** extended — allows `"Arrived"`
   as a third valid target status so staff can override `NoShow → Arrived`.

5. **`UpdateAppointmentStatusCommandHandler`** extended — adds
   `NoShow → Arrived` to the `AllowedTransitions` dictionary.

---

## Acceptance Criteria Covered

- AC: Staff can mark appointment as NoShow if patient doesn't arrive within 30 min of slot end
- AC: No-show triggers follow-up email ("We missed you, please reschedule")
- AC: No-show count tracked on patient profile (lifetime)
- AC: No-show appointment frees slot only after marking (not auto-freed before)
- AC: Audit log entry for no-show marking *(auto via AuditSaveChangesInterceptor)*
- AC: Admin report: no-show rate by provider, by day of week, by time slot
- EC: Patient arrives after auto-marking → staff override NoShow → Arrived

---

## Implementation Steps

### 1. Create `ActiveUnattendedPastCutoffSpecification`

Create new file:
`src/HealthPlatform.Application/Features/Appointments/ActiveUnattendedPastCutoffSpecification.cs`

```csharp
using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Returns all Scheduled or Booked appointments whose slot ended at least
/// <paramref name="cutoffUtc"/> ago and whose patient never checked in
/// (ArrivalTime is null).  Used by the Hangfire auto-mark job to find
/// appointments eligible for automatic no-show marking.
///
/// Eagerly loads Slot (for EndTime) and Patient (for email + profile update).
/// </summary>
internal sealed class ActiveUnattendedPastCutoffSpecification : ISpecification<Appointment>
{
    private readonly DateTimeOffset _cutoffUtc;

    public ActiveUnattendedPastCutoffSpecification(DateTimeOffset cutoffUtc)
        => _cutoffUtc = cutoffUtc;

    public Expression<Func<Appointment, bool>>? Criteria =>
        a => (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Booked)
          && a.ArrivalTime == null
          && a.SlotTime <= _cutoffUtc;

    public List<Expression<Func<Appointment, object>>> Includes =>
    [
        a => a.Slot!,
        a => a.Patient,
    ];

    public Expression<Func<Appointment, object>>? OrderBy           => null;
    public Expression<Func<Appointment, object>>? OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
```

> **Note:** `SlotTime` is used as the proxy for slot end time.  The Hangfire
> job passes `DateTimeOffset.UtcNow.AddMinutes(-30)` as `cutoffUtc`, so only
> appointments whose slot started ≥ 30 min ago are selected.  This is a
> conservative approximation; slots are typically 30 min, so the patient
> would be at least 30 min past slot start (effectively past the slot end).
> If `AppointmentSlot.EndTime` is needed for precision, the job can load
> slots separately — but `SlotTime` is sufficient for the stated AC.

---

### 2. Create `MarkNoShowCommand` (command record + DTOs)

Create new file:
`src/HealthPlatform.Application/Features/Appointments/MarkNoShowCommand.cs`

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Marks a single appointment as <see cref="Domain.Enums.AppointmentStatus.NoShow"/>.
///
/// <paramref name="IsAutomatic"/> distinguishes Hangfire auto-marking
/// (no authentication context) from manual staff action (authenticated).
/// The distinction is recorded in the audit log via the modified entity fields.
/// </summary>
public sealed record MarkNoShowCommand(
    Guid AppointmentId,
    bool IsAutomatic = false) : IRequest<NoShowConfirmationDto>;

/// <summary>
/// Returned by <see cref="MarkNoShowCommand"/> so the API controller can
/// broadcast a SignalR notification to the relevant provider group.
/// </summary>
public sealed record NoShowConfirmationDto(
    Guid            AppointmentId,
    Guid            PatientId,
    Guid            ProviderId,
    DateTimeOffset  SlotTime,
    bool            IsAutomatic,
    int             PatientTotalNoShowCount);
```

---

### 3. Create `MarkNoShowCommandValidator`

Create new file:
`src/HealthPlatform.Application/Features/Appointments/MarkNoShowCommandValidator.cs`

```csharp
using FluentValidation;

namespace HealthPlatform.Application.Features.Appointments;

internal sealed class MarkNoShowCommandValidator : AbstractValidator<MarkNoShowCommand>
{
    public MarkNoShowCommandValidator()
    {
        RuleFor(c => c.AppointmentId).NotEmpty();
    }
}
```

---

### 4. Create `MarkNoShowCommandHandler`

Create new file:
`src/HealthPlatform.Application/Features/Appointments/MarkNoShowCommandHandler.cs`

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Handles <see cref="MarkNoShowCommand"/>.
///
/// Flow:
///  1. Load appointment with Slot + Patient navigations.
///  2. Status guard: only Scheduled or Booked appointments can become NoShow.
///     (Arrived/InProgress patients are present; Completed/Cancelled/NoShow are terminal.)
///  3. Mutate appointment status → NoShow.
///  4. Free the associated slot back to Available.
///  5. Increment patient's lifetime no-show counter.
///  6. SaveChanges — AuditSaveChangesInterceptor writes the audit log entry.
///  7. Send follow-up email to patient.
///  8. Return DTO with ProviderId for SignalR broadcast.
/// </summary>
internal sealed class MarkNoShowCommandHandler
    : IRequestHandler<MarkNoShowCommand, NoShowConfirmationDto>
{
    private readonly IUnitOfWork  _uow;
    private readonly IEmailSender _emailSender;

    public MarkNoShowCommandHandler(IUnitOfWork uow, IEmailSender emailSender)
    {
        _uow         = uow;
        _emailSender = emailSender;
    }

    public async Task<NoShowConfirmationDto> Handle(
        MarkNoShowCommand command,
        CancellationToken ct)
    {
        // ── 1. Load appointment with Slot + Patient ───────────────────────
        var results = await _uow.Repository<Appointment>()
            .GetAsync(new AppointmentByIdWithSlotAndPatientSpecification(command.AppointmentId), ct);

        if (results.Count == 0)
            throw new NotFoundException(nameof(Appointment), command.AppointmentId);

        var appointment = results[0];

        // ── 2. Status guard ───────────────────────────────────────────────
        if (appointment.Status is not (AppointmentStatus.Scheduled or AppointmentStatus.Booked))
        {
            throw new ArgumentException(
                $"Cannot mark appointment as NoShow: current status is '{appointment.Status}'. " +
                "Only Scheduled or Booked appointments may be marked as NoShow.");
        }

        // ── 3. Mutate appointment status ──────────────────────────────────
        appointment.Status = AppointmentStatus.NoShow;
        _uow.Repository<Appointment>().Update(appointment);

        // ── 4. Free the associated slot ───────────────────────────────────
        if (appointment.Slot is not null)
        {
            appointment.Slot.Status = SlotStatus.Available;
            _uow.Repository<AppointmentSlot>().Update(appointment.Slot);
        }

        // ── 5. Increment lifetime no-show count on patient profile ────────
        appointment.Patient.TotalNoShowCount++;
        _uow.Repository<PatientProfile>().Update(appointment.Patient);

        // ── 6. Persist (interceptor auto-writes audit log) ────────────────
        await _uow.SaveChangesAsync(ct);

        // ── 7. Send follow-up email ───────────────────────────────────────
        var patientEmail = appointment.Patient.User?.Email;
        if (!string.IsNullOrWhiteSpace(patientEmail))
        {
            await _emailSender.SendAsync(
                toAddress: patientEmail,
                subject:   "We missed you — please reschedule your appointment",
                body:      $"Dear {appointment.Patient.FirstName},\n\n" +
                           $"We noticed you missed your appointment scheduled for " +
                           $"{appointment.SlotTime:f} UTC. " +
                           "Please contact us or visit our portal to reschedule at your earliest convenience.\n\n" +
                           "We look forward to seeing you soon.",
                ct:        ct);
        }

        return new NoShowConfirmationDto(
            AppointmentId:          appointment.Id,
            PatientId:              appointment.PatientId,
            ProviderId:             appointment.ProviderId,
            SlotTime:               appointment.SlotTime,
            IsAutomatic:            command.IsAutomatic,
            PatientTotalNoShowCount: appointment.Patient.TotalNoShowCount);
    }
}
```

> **Navigation note:** `AppointmentByIdWithSlotAndPatientSpecification` already
> includes `a => a.Slot!` and `a => a.Patient`.  `a.Patient.User` is a further
> level required for the email address.  Either extend the specification to
> also include `a => a.Patient.User` or use a secondary lookup.
>
> **Recommended action:** Edit
> `src/HealthPlatform.Application/Features/Appointments/AppointmentByIdWithSlotAndPatientSpecification.cs`
> to add `a => a.Patient.User` to the `Includes` list:
>
> ```csharp
> public List<Expression<Func<Appointment, object>>> Includes =>
> [
>     a => a.Slot!,
>     a => a.Patient,
>     a => a.Patient.User,   // ← add this line
> ];
> ```

---

### 5. Create `GetNoShowReportQuery` (query record + DTOs)

Create new file:
`src/HealthPlatform.Application/Features/Appointments/GetNoShowReportQuery.cs`

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Returns a no-show analytics report for a date range, aggregating by
/// provider, day of week, and time slot.  Optionally filtered to a single
/// provider via <paramref name="ProviderId"/>.
/// </summary>
public sealed record GetNoShowReportQuery(
    DateOnly  DateFrom,
    DateOnly  DateTo,
    Guid?     ProviderId = null) : IRequest<NoShowReportDto>;

public sealed record NoShowReportDto(
    IReadOnlyList<NoShowByProviderDto>    ByProvider,
    IReadOnlyList<NoShowByDayOfWeekDto>   ByDayOfWeek,
    IReadOnlyList<NoShowByTimeSlotDto>    ByTimeSlot,
    int                                   TotalAppointments,
    int                                   TotalNoShows,
    double                                OverallNoShowRate);

public sealed record NoShowByProviderDto(
    Guid   ProviderId,
    string ProviderName,
    int    TotalAppointments,
    int    NoShowCount,
    double NoShowRate);

public sealed record NoShowByDayOfWeekDto(
    DayOfWeek DayOfWeek,
    string    DayName,
    int       TotalAppointments,
    int       NoShowCount,
    double    NoShowRate);

public sealed record NoShowByTimeSlotDto(
    int    HourUtc,
    string SlotLabel,
    int    TotalAppointments,
    int    NoShowCount,
    double NoShowRate);
```

---

### 6. Create `NoShowReportSpecification`

Create new file:
`src/HealthPlatform.Application/Features/Appointments/NoShowReportSpecification.cs`

```csharp
using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Fetches all appointments in a date range that are either Completed or
/// NoShow (the denominator + numerator for the no-show rate).  Eagerly
/// loads the Provider navigation for grouping.
/// </summary>
internal sealed class NoShowReportSpecification : ISpecification<Appointment>
{
    private readonly DateTimeOffset _from;
    private readonly DateTimeOffset _to;
    private readonly Guid?          _providerId;

    public NoShowReportSpecification(DateOnly dateFrom, DateOnly dateTo, Guid? providerId)
    {
        _from       = new DateTimeOffset(dateFrom.Year, dateFrom.Month, dateFrom.Day,  0,  0,  0, TimeSpan.Zero);
        _to         = new DateTimeOffset(dateTo.Year,   dateTo.Month,   dateTo.Day,   23, 59, 59, TimeSpan.Zero);
        _providerId = providerId;
    }

    public Expression<Func<Appointment, bool>>? Criteria =>
        a => a.SlotTime >= _from
          && a.SlotTime <= _to
          && (a.Status == AppointmentStatus.Completed || a.Status == AppointmentStatus.NoShow)
          && (_providerId == null || a.ProviderId == _providerId.Value);

    public List<Expression<Func<Appointment, object>>> Includes =>
    [
        a => a.Provider,
    ];

    public Expression<Func<Appointment, object>>? OrderBy           => null;
    public Expression<Func<Appointment, object>>? OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
```

---

### 7. Create `GetNoShowReportQueryHandler`

Create new file:
`src/HealthPlatform.Application/Features/Appointments/GetNoShowReportQueryHandler.cs`

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Handles <see cref="GetNoShowReportQuery"/>.
///
/// Loads all Completed + NoShow appointments in the requested date range,
/// then aggregates in-memory.  Not paginated — reports are expected to
/// cover bounded date ranges (max 90 days enforced by the validator).
/// </summary>
internal sealed class GetNoShowReportQueryHandler
    : IRequestHandler<GetNoShowReportQuery, NoShowReportDto>
{
    private readonly IUnitOfWork _uow;

    public GetNoShowReportQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<NoShowReportDto> Handle(
        GetNoShowReportQuery query,
        CancellationToken    ct)
    {
        var appointments = await _uow.Repository<Appointment>()
            .GetAsync(new NoShowReportSpecification(query.DateFrom, query.DateTo, query.ProviderId), ct);

        var totalAppointments = appointments.Count;
        var totalNoShows      = appointments.Count(a => a.Status == Domain.Enums.AppointmentStatus.NoShow);
        var overallRate       = totalAppointments == 0
            ? 0.0
            : Math.Round((double)totalNoShows / totalAppointments * 100, 2);

        // ── By Provider ───────────────────────────────────────────────────
        var byProvider = appointments
            .GroupBy(a => a.ProviderId)
            .Select(g =>
            {
                var provider  = g.First().Provider;
                var total     = g.Count();
                var noShows   = g.Count(a => a.Status == Domain.Enums.AppointmentStatus.NoShow);
                var rate      = total == 0 ? 0.0 : Math.Round((double)noShows / total * 100, 2);
                return new NoShowByProviderDto(
                    ProviderId:         g.Key,
                    ProviderName:       $"{provider.FirstName} {provider.LastName}",
                    TotalAppointments:  total,
                    NoShowCount:        noShows,
                    NoShowRate:         rate);
            })
            .OrderByDescending(x => x.NoShowRate)
            .ToList();

        // ── By Day of Week ────────────────────────────────────────────────
        var byDayOfWeek = appointments
            .GroupBy(a => a.SlotTime.DayOfWeek)
            .Select(g =>
            {
                var total   = g.Count();
                var noShows = g.Count(a => a.Status == Domain.Enums.AppointmentStatus.NoShow);
                var rate    = total == 0 ? 0.0 : Math.Round((double)noShows / total * 100, 2);
                return new NoShowByDayOfWeekDto(
                    DayOfWeek:         g.Key,
                    DayName:           g.Key.ToString(),
                    TotalAppointments: total,
                    NoShowCount:       noShows,
                    NoShowRate:        rate);
            })
            .OrderBy(x => x.DayOfWeek)
            .ToList();

        // ── By Time Slot (hour bucket) ────────────────────────────────────
        var byTimeSlot = appointments
            .GroupBy(a => a.SlotTime.Hour)
            .Select(g =>
            {
                var hour    = g.Key;
                var total   = g.Count();
                var noShows = g.Count(a => a.Status == Domain.Enums.AppointmentStatus.NoShow);
                var rate    = total == 0 ? 0.0 : Math.Round((double)noShows / total * 100, 2);
                return new NoShowByTimeSlotDto(
                    HourUtc:           hour,
                    SlotLabel:         $"{hour:D2}:00–{hour:D2}:59 UTC",
                    TotalAppointments: total,
                    NoShowCount:       noShows,
                    NoShowRate:        rate);
            })
            .OrderBy(x => x.HourUtc)
            .ToList();

        return new NoShowReportDto(
            ByProvider:        byProvider,
            ByDayOfWeek:       byDayOfWeek,
            ByTimeSlot:        byTimeSlot,
            TotalAppointments: totalAppointments,
            TotalNoShows:      totalNoShows,
            OverallNoShowRate: overallRate);
    }
}
```

---

### 8. Create `GetNoShowReportQueryValidator`

Create new file:
`src/HealthPlatform.Application/Features/Appointments/GetNoShowReportQueryValidator.cs`

```csharp
using FluentValidation;

namespace HealthPlatform.Application.Features.Appointments;

internal sealed class GetNoShowReportQueryValidator : AbstractValidator<GetNoShowReportQuery>
{
    public GetNoShowReportQueryValidator()
    {
        RuleFor(q => q.DateFrom).NotEmpty();
        RuleFor(q => q.DateTo)
            .NotEmpty()
            .GreaterThanOrEqualTo(q => q.DateFrom)
            .WithMessage("DateTo must be on or after DateFrom.");
        RuleFor(q => q)
            .Must(q => (q.DateTo.DayNumber - q.DateFrom.DayNumber) <= 90)
            .WithMessage("Report date range cannot exceed 90 days.");
    }
}
```

---

### 9. Extend `UpdateAppointmentStatusCommandValidator` for NoShow → Arrived

Edit `src/HealthPlatform.Application/Features/Appointments/UpdateAppointmentStatusCommandValidator.cs`.

Change:

```csharp
    private static readonly string[] AllowedTargetStatuses = ["InProgress", "Completed"];
```

To:

```csharp
    private static readonly string[] AllowedTargetStatuses = ["InProgress", "Completed", "Arrived"];
```

And update the error message `WithMessage(...)` to reflect the additional option:

```csharp
            .WithMessage(
                $"Allowed target statuses are: {string.Join(", ", AllowedTargetStatuses)}.");
```

*(The message text already uses the array, so this updates automatically.)*

---

### 10. Extend `UpdateAppointmentStatusCommandHandler` for NoShow → Arrived

Edit `src/HealthPlatform.Application/Features/Appointments/UpdateAppointmentStatusCommandHandler.cs`.

Change `AllowedTransitions` to:

```csharp
    private static readonly Dictionary<AppointmentStatus, AppointmentStatus> AllowedTransitions =
        new()
        {
            [AppointmentStatus.Arrived]    = AppointmentStatus.InProgress,
            [AppointmentStatus.InProgress] = AppointmentStatus.Completed,
            [AppointmentStatus.NoShow]     = AppointmentStatus.Arrived,   // staff override
        };
```

Update the error message in the transition guard to mention the new path:

```csharp
                "Allowed transitions: Arrived → InProgress, InProgress → Completed, NoShow → Arrived.");
```

Also, when `NoShow → Arrived` occurs, set `ArrivalTime` so the patient profile
shows a check-in time:

After `appointment.Status = target;` add:

```csharp
        if (oldStatus == AppointmentStatus.NoShow.ToString() && target == AppointmentStatus.Arrived)
            appointment.ArrivalTime = DateTimeOffset.UtcNow;
```

*(Note: `oldStatus` is already captured before mutation in the existing handler.)*

---

## Files Modified / Created

| Path | Action |
|------|--------|
| `src/HealthPlatform.Application/Features/Appointments/ActiveUnattendedPastCutoffSpecification.cs` | Create |
| `src/HealthPlatform.Application/Features/Appointments/MarkNoShowCommand.cs` | Create |
| `src/HealthPlatform.Application/Features/Appointments/MarkNoShowCommandValidator.cs` | Create |
| `src/HealthPlatform.Application/Features/Appointments/MarkNoShowCommandHandler.cs` | Create |
| `src/HealthPlatform.Application/Features/Appointments/AppointmentByIdWithSlotAndPatientSpecification.cs` | Edit — add `a => a.Patient.User` include |
| `src/HealthPlatform.Application/Features/Appointments/GetNoShowReportQuery.cs` | Create |
| `src/HealthPlatform.Application/Features/Appointments/NoShowReportSpecification.cs` | Create |
| `src/HealthPlatform.Application/Features/Appointments/GetNoShowReportQueryHandler.cs` | Create |
| `src/HealthPlatform.Application/Features/Appointments/GetNoShowReportQueryValidator.cs` | Create |
| `src/HealthPlatform.Application/Features/Appointments/UpdateAppointmentStatusCommandValidator.cs` | Edit — allow `"Arrived"` |
| `src/HealthPlatform.Application/Features/Appointments/UpdateAppointmentStatusCommandHandler.cs` | Edit — add `NoShow → Arrived` transition + `ArrivalTime` set |

## Verification

- [ ] `dotnet build src/HealthPlatform.sln` passes with no errors
- [ ] `MarkNoShowCommandHandler` throws `NotFoundException` for unknown appointment ID
- [ ] `MarkNoShowCommandHandler` throws `ArgumentException` when appointment status is `Arrived` or `Completed`
- [ ] `MarkNoShowCommandHandler` sets `Slot.Status = Available` and increments `Patient.TotalNoShowCount`
- [ ] `GetNoShowReportQueryValidator` rejects date ranges exceeding 90 days
- [ ] `UpdateAppointmentStatusCommand` with `"Arrived"` on a NoShow appointment succeeds
- [ ] `UpdateAppointmentStatusCommand` with `"Arrived"` on a Booked appointment still fails (guard)
