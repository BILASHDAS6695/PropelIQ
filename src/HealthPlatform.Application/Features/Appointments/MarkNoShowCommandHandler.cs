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
///  1. Load appointment with Slot + Patient + Patient.User navigations.
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
        // ── 1. Load appointment with Slot + Patient + Patient.User ────────
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
            AppointmentId:           appointment.Id,
            PatientId:               appointment.PatientId,
            ProviderId:              appointment.ProviderId,
            SlotTime:                appointment.SlotTime,
            IsAutomatic:             command.IsAutomatic,
            PatientTotalNoShowCount: appointment.Patient.TotalNoShowCount);
    }
}
