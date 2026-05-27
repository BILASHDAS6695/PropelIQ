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
    private readonly IReminderScheduler  _reminders;

    public RescheduleAppointmentCommandHandler(
        IUnitOfWork         uow,
        ICurrentUserService currentUser,
        IEmailSender        emailSender,
        IReminderScheduler  reminders)
    {
        _uow         = uow;
        _currentUser = currentUser;
        _emailSender = emailSender;
        _reminders   = reminders;
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
        //    Edge case: if the slot is taken, the existing appointment must
        //    NOT be cancelled — verified before any mutation.
        var newSlot = await _uow.Repository<AppointmentSlot>()
            .GetByIdAsync(command.NewSlotId, ct)
            ?? throw new NotFoundException(nameof(AppointmentSlot), command.NewSlotId);

        if (newSlot.Status != SlotStatus.Available)
            throw new ConflictException(
                "The requested slot is no longer available. " +
                "Your existing appointment has not been cancelled.");

        // ── 6. Load provider for confirmation DTO ─────────────────────────
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

        // ── 9. Cancel pending reminders for old appointment + persist ─────
        // Cancel() nulls job IDs in-memory; SaveChanges commits the slot
        // mutations, status change, and null job IDs in one round-trip.
        _reminders.Cancel(existing);

        await _uow.SaveChangesAsync(ct);

        // ── 10. Send reschedule confirmation email ────────────────────────
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

        // ── 11. Schedule reminders for new appointment ────────────────────
        await _reminders.ScheduleAsync(newAppointment, ct);

        return new RescheduleConfirmationDto(
            existing.Id,
            newAppointment.Id,
            newAppointment.SlotTime,
            newAppointment.Status.ToString());
    }
}
