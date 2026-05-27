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
            [AppointmentStatus.NoShow]     = AppointmentStatus.Arrived,   // staff override for late arrivals
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
                "Allowed transitions: Arrived → InProgress, InProgress → Completed, NoShow → Arrived.");
        }

        // ── 4. Mutate ─────────────────────────────────────────────────────
        var oldStatus = appointment.Status.ToString();
        appointment.Status = target;
        if (appointment.Status == AppointmentStatus.Arrived
            && oldStatus == AppointmentStatus.NoShow.ToString())
        {
            appointment.ArrivalTime = DateTimeOffset.UtcNow;
        }
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
