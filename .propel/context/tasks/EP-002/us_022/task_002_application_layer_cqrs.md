# Task 002: Application Layer — CQRS Commands

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-022 |
| **Epic** | EP-002 |
| **Layer** | Application (CQRS handlers, specifications) |
| **Priority** | High |
| **Estimated Effort** | 90 minutes |
| **Dependencies** | Task 001 (`CancellationReason` enum, `Appointment.CancellationReason?`, `Appointment.CancellationNote?`) |

## Objective

Implement two commands — `CancelAppointmentCommand` and
`RescheduleAppointmentCommand` — that enforce the US-022 business rules:
the 2-hour patient restriction, the Arrived-status guard, staff bypass, atomic
reschedule (cancel + rebook), and confirmation email delivery.  Audit log entries
are produced automatically by the existing `AuditSaveChangesInterceptor`; no
explicit logging code is required here.

## Acceptance Criteria Covered

- AC: Patient can cancel appointment up to 2 hours before start time
- AC: Cancellation changes appointment status to "Cancelled" and slot status back to "Available"
- AC: Cancellation reason required
- AC: Cancellation confirmation email sent to patient
- AC: Reschedule = cancel + rebook in one flow (preserves visit reason)
- AC: Staff can cancel any appointment regardless of time restriction
- AC: Cancelled slots immediately available for other patients (real-time)
- AC: Audit log entry for cancellation/reschedule with reason
- Edge: Patient tries to cancel < 2 h before → HTTP 400
- Edge: Patient cancels Arrived appointment → HTTP 400
- Edge: Reschedule but no slots available → cancel portion not executed

---

## Implementation Steps

### 1. `AppointmentByIdWithSlotAndPatientSpecification`

Create `src/HealthPlatform.Application/Features/Appointments/AppointmentByIdWithSlotAndPatientSpecification.cs`:

```csharp
using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Loads a single appointment by primary key, eagerly including
/// its optional Slot and its owning Patient.  Used by the
/// cancel and reschedule handlers to avoid separate round-trips.
/// </summary>
internal sealed class AppointmentByIdWithSlotAndPatientSpecification
    : ISpecification<Appointment>
{
    private readonly Guid _appointmentId;

    public AppointmentByIdWithSlotAndPatientSpecification(Guid appointmentId)
        => _appointmentId = appointmentId;

    public Expression<Func<Appointment, bool>>? Criteria =>
        a => a.Id == _appointmentId;

    public List<Expression<Func<Appointment, object>>> Includes =>
    [
        a => a.Slot!,
        a => a.Patient
    ];

    public Expression<Func<Appointment, object>>?      OrderBy           => null;
    public Expression<Func<Appointment, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
```

---

### 2. `CancelAppointmentCommand` + Validator + Handler + DTO

#### 2a. Command & DTO

Create `src/HealthPlatform.Application/Features/Appointments/CancelAppointmentCommand.cs`:

```csharp
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Cancels an existing appointment.
/// Patients may only cancel their own appointment and only if the start time
/// is more than 2 hours away.  Staff bypass the time restriction and may
/// cancel any appointment.
/// </summary>
public sealed record CancelAppointmentCommand(
    Guid               AppointmentId,
    CancellationReason Reason,
    string?            Note,
    bool               CallerIsStaff) : IRequest<CancellationConfirmationDto>;

public sealed record CancellationConfirmationDto(
    Guid   AppointmentId,
    string Status,
    string CancellationReason);
```

#### 2b. Validator

Create `src/HealthPlatform.Application/Features/Appointments/CancelAppointmentCommandValidator.cs`:

```csharp
using FluentValidation;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.Appointments;

public sealed class CancelAppointmentCommandValidator
    : AbstractValidator<CancelAppointmentCommand>
{
    public CancelAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();

        RuleFor(x => x.Reason)
            .IsInEnum()
            .WithMessage("Cancellation reason must be one of: ScheduleConflict, FeelingBetter, Other.");

        // Note is required in the UI when Reason = Other; enforce it here too.
        RuleFor(x => x.Note)
            .NotEmpty()
            .WithMessage("A cancellation note is required when the reason is Other.")
            .When(x => x.Reason == CancellationReason.Other);
    }
}
```

#### 2c. Handler

Create `src/HealthPlatform.Application/Features/Appointments/CancelAppointmentCommandHandler.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Cancels an appointment.
///
/// Flow:
/// 1. Authenticate caller.
/// 2. Load appointment with Slot + Patient via specification.
/// 3. Ownership check: non-staff callers must own the appointment.
/// 4. Guard: Arrived appointments cannot be cancelled.
/// 5. Time guard: non-staff callers blocked within 2 hours of start time.
/// 6. Mutate status → Cancelled; set CancellationReason + CancellationNote.
/// 7. Free the associated slot (if any) back to Available.
/// 8. SaveChanges — AuditSaveChangesInterceptor writes the audit log entry.
/// 9. Send cancellation confirmation email.
/// </summary>
internal sealed class CancelAppointmentCommandHandler
    : IRequestHandler<CancelAppointmentCommand, CancellationConfirmationDto>
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailSender        _emailSender;

    public CancelAppointmentCommandHandler(
        IUnitOfWork         uow,
        ICurrentUserService currentUser,
        IEmailSender        emailSender)
    {
        _uow         = uow;
        _currentUser = currentUser;
        _emailSender = emailSender;
    }

    public async Task<CancellationConfirmationDto> Handle(
        CancelAppointmentCommand command,
        CancellationToken        ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("User must be authenticated to cancel appointments.");

        // ── 1. Load appointment with navigations ──────────────────────────
        var results = await _uow.Repository<Appointment>()
            .GetAsync(new AppointmentByIdWithSlotAndPatientSpecification(command.AppointmentId), ct);

        if (results.Count == 0)
            throw new NotFoundException(nameof(Appointment), command.AppointmentId);

        var appointment = results[0];

        // ── 2. Ownership check (skipped for staff) ────────────────────────
        if (!command.CallerIsStaff
            && appointment.Patient.UserId != _currentUser.UserId.Value)
        {
            throw new ForbiddenAccessException(
                "You do not have permission to cancel this appointment.");
        }

        // ── 3. Status guard ───────────────────────────────────────────────
        if (appointment.Status == AppointmentStatus.Arrived)
            throw new ArgumentException(
                "Cannot cancel an appointment that has already been marked as Arrived.");

        if (appointment.Status == AppointmentStatus.Cancelled)
            throw new ArgumentException("This appointment has already been cancelled.");

        if (appointment.Status == AppointmentStatus.Completed)
            throw new ArgumentException("Cannot cancel a completed appointment.");

        // ── 4. Time restriction (patients only) ───────────────────────────
        if (!command.CallerIsStaff)
        {
            var minutesUntilStart = (appointment.SlotTime - DateTimeOffset.UtcNow).TotalMinutes;
            if (minutesUntilStart < 120)
                throw new ArgumentException(
                    "Too late to cancel online. Please call the clinic to cancel.");
        }

        // ── 5. Mutate appointment ──────────────────────────────────────────
        appointment.Status             = AppointmentStatus.Cancelled;
        appointment.CancellationReason = command.Reason;
        appointment.CancellationNote   = command.Note;
        _uow.Repository<Appointment>().Update(appointment);

        // ── 6. Free associated slot ───────────────────────────────────────
        if (appointment.Slot is not null)
        {
            appointment.Slot.Status = SlotStatus.Available;
            _uow.Repository<AppointmentSlot>().Update(appointment.Slot);
        }

        // ── 7. Persist (interceptor auto-writes audit log) ────────────────
        await _uow.SaveChangesAsync(ct);

        // ── 8. Send cancellation email ────────────────────────────────────
        var user = await _uow.Repository<User>()
            .GetByIdAsync(appointment.Patient.UserId, ct);

        if (user is not null)
        {
            var emailBody =
                $"Your appointment on {appointment.SlotTime:f} UTC has been cancelled.\n" +
                $"Reason: {appointment.CancellationReason}\n" +
                $"Appointment ID: {appointment.Id}\n\n" +
                "If you did not request this cancellation, please contact the clinic.";

            await _emailSender.SendAsync(
                user.Email,
                "Appointment Cancellation Confirmation",
                emailBody,
                ct);
        }

        return new CancellationConfirmationDto(
            appointment.Id,
            appointment.Status.ToString(),
            appointment.CancellationReason!.Value.ToString());
    }
}
```

---

### 3. `RescheduleAppointmentCommand` + Validator + Handler + DTO

#### 3a. Command & DTO

Create `src/HealthPlatform.Application/Features/Appointments/RescheduleAppointmentCommand.cs`:

```csharp
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Reschedules an appointment: cancels the existing booking and creates a
/// new one on the requested slot — atomically within a single SaveChanges.
/// The original visit reason is preserved on the new appointment.
/// If the new slot is unavailable, the current appointment is NOT cancelled.
/// </summary>
public sealed record RescheduleAppointmentCommand(
    Guid               AppointmentId,
    Guid               NewSlotId,
    CancellationReason Reason,
    string?            Note,
    bool               CallerIsStaff) : IRequest<RescheduleConfirmationDto>;

public sealed record RescheduleConfirmationDto(
    Guid           OldAppointmentId,
    Guid           NewAppointmentId,
    DateTimeOffset NewAppointmentTime,
    string         Status);
```

#### 3b. Validator

Create `src/HealthPlatform.Application/Features/Appointments/RescheduleAppointmentCommandValidator.cs`:

```csharp
using FluentValidation;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.Appointments;

public sealed class RescheduleAppointmentCommandValidator
    : AbstractValidator<RescheduleAppointmentCommand>
{
    public RescheduleAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.NewSlotId).NotEmpty();

        RuleFor(x => x.Reason)
            .IsInEnum()
            .WithMessage("Cancellation reason must be one of: ScheduleConflict, FeelingBetter, Other.");

        RuleFor(x => x.Note)
            .NotEmpty()
            .WithMessage("A note is required when the reason is Other.")
            .When(x => x.Reason == CancellationReason.Other);
    }
}
```

#### 3c. Handler

Create `src/HealthPlatform.Application/Features/Appointments/RescheduleAppointmentCommandHandler.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Reschedules an appointment atomically.
///
/// Flow:
/// 1. Authenticate caller.
/// 2. Load existing appointment with Slot + Patient.
/// 3. Ownership check (same rules as cancellation).
/// 4. Status + time guards (same rules as cancellation).
/// 5. Load new slot — verify it is Available.
///    → If unavailable: throw ConflictException BEFORE touching the old appointment.
/// 6. Mutate old appointment to Cancelled; free its slot.
/// 7. Create new appointment (Status = Scheduled, same VisitReason).
/// 8. Mark new slot as Booked.
/// 9. SaveChanges — both mutations land in the same DB transaction.
/// 10. Send reschedule confirmation email.
/// </summary>
internal sealed class RescheduleAppointmentCommandHandler
    : IRequestHandler<RescheduleAppointmentCommand, RescheduleConfirmationDto>
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailSender        _emailSender;

    public RescheduleAppointmentCommandHandler(
        IUnitOfWork         uow,
        ICurrentUserService currentUser,
        IEmailSender        emailSender)
    {
        _uow         = uow;
        _currentUser = currentUser;
        _emailSender = emailSender;
    }

    public async Task<RescheduleConfirmationDto> Handle(
        RescheduleAppointmentCommand command,
        CancellationToken            ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("User must be authenticated to reschedule appointments.");

        // ── 1. Load existing appointment ──────────────────────────────────
        var results = await _uow.Repository<Appointment>()
            .GetAsync(new AppointmentByIdWithSlotAndPatientSpecification(command.AppointmentId), ct);

        if (results.Count == 0)
            throw new NotFoundException(nameof(Appointment), command.AppointmentId);

        var existing = results[0];

        // ── 2. Ownership check ────────────────────────────────────────────
        if (!command.CallerIsStaff
            && existing.Patient.UserId != _currentUser.UserId.Value)
        {
            throw new ForbiddenAccessException(
                "You do not have permission to reschedule this appointment.");
        }

        // ── 3. Status guard ───────────────────────────────────────────────
        if (existing.Status == AppointmentStatus.Arrived)
            throw new ArgumentException(
                "Cannot reschedule an appointment that has already been marked as Arrived.");

        if (existing.Status == AppointmentStatus.Cancelled)
            throw new ArgumentException("This appointment has already been cancelled.");

        if (existing.Status == AppointmentStatus.Completed)
            throw new ArgumentException("Cannot reschedule a completed appointment.");

        // ── 4. Time restriction (patients only) ───────────────────────────
        if (!command.CallerIsStaff)
        {
            var minutesUntilStart = (existing.SlotTime - DateTimeOffset.UtcNow).TotalMinutes;
            if (minutesUntilStart < 120)
                throw new ArgumentException(
                    "Too late to reschedule online. Please call the clinic.");
        }

        // ── 5. Verify new slot is available BEFORE cancelling ─────────────
        //    Edge case: if no available slot exists, the existing appointment
        //    must NOT be cancelled.
        var newSlot = await _uow.Repository<AppointmentSlot>()
            .GetByIdAsync(command.NewSlotId, ct)
            ?? throw new NotFoundException(nameof(AppointmentSlot), command.NewSlotId);

        if (newSlot.Status != SlotStatus.Available)
            throw new ConflictException(
                "The requested slot is no longer available. " +
                "Your existing appointment has not been cancelled.");

        // ── 6. Load provider for DTO ──────────────────────────────────────
        var provider = await _uow.Repository<Provider>()
            .GetByIdAsync(newSlot.ProviderId, ct)
            ?? throw new NotFoundException(nameof(Provider), newSlot.ProviderId);

        // ── 7. Cancel old appointment + free old slot ─────────────────────
        existing.Status             = AppointmentStatus.Cancelled;
        existing.CancellationReason = command.Reason;
        existing.CancellationNote   = command.Note;
        _uow.Repository<Appointment>().Update(existing);

        if (existing.Slot is not null)
        {
            existing.Slot.Status = SlotStatus.Available;
            _uow.Repository<AppointmentSlot>().Update(existing.Slot);
        }

        // ── 8. Create new appointment + mark new slot Booked ──────────────
        var newAppointment = new Appointment
        {
            Id          = Guid.NewGuid(),
            PatientId   = existing.PatientId,
            ProviderId  = newSlot.ProviderId,
            SlotId      = newSlot.Id,
            SlotTime    = newSlot.StartTime,
            Status      = AppointmentStatus.Scheduled,
            VisitReason = existing.VisitReason,   // preserve visit reason
            IsWalkIn    = false
        };

        newSlot.Status = SlotStatus.Booked;
        _uow.Repository<AppointmentSlot>().Update(newSlot);
        await _uow.Repository<Appointment>().AddAsync(newAppointment, ct);

        // ── 9. Persist atomically ─────────────────────────────────────────
        await _uow.SaveChangesAsync(ct);

        // ── 10. Send reschedule email ─────────────────────────────────────
        var user = await _uow.Repository<User>()
            .GetByIdAsync(existing.Patient.UserId, ct);

        if (user is not null)
        {
            var emailBody =
                $"Your appointment has been rescheduled.\n" +
                $"New Date & Time: {newAppointment.SlotTime:f} UTC\n" +
                $"Provider: {provider.Name}\n" +
                $"New Appointment ID: {newAppointment.Id}\n" +
                $"Previous Appointment ID: {existing.Id}";

            await _emailSender.SendAsync(
                user.Email,
                "Appointment Rescheduled",
                emailBody,
                ct);
        }

        return new RescheduleConfirmationDto(
            existing.Id,
            newAppointment.Id,
            newAppointment.SlotTime,
            newAppointment.Status.ToString());
    }
}
```

---

## Verification Checklist

- [ ] `AppointmentByIdWithSlotAndPatientSpecification` includes both `Slot` and `Patient` navigations
- [ ] `CancelAppointmentCommandValidator` requires `Note` when `Reason == Other`
- [ ] Cancellation handler throws `ArgumentException` (HTTP 400) for Arrived guard
- [ ] Cancellation handler throws `ArgumentException` (HTTP 400) when < 2 h remaining (patient only)
- [ ] Reschedule handler verifies new slot availability **before** mutating the existing appointment
- [ ] Reschedule new appointment preserves `VisitReason` from the old appointment
- [ ] Both handlers free the old slot status back to `Available`
- [ ] Both handlers call `IEmailSender.SendAsync` with the patient's email
- [ ] `dotnet build src/HealthPlatform.sln` compiles without errors
