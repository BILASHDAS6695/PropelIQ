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
