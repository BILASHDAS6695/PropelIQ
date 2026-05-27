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
