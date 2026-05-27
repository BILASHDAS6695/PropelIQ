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
