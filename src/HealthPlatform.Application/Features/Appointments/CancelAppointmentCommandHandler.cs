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

        // ── 5. Mutate appointment ─────────────────────────────────────────
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
