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
