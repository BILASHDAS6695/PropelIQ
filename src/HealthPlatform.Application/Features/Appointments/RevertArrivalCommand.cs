using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Reverts an accidental check-in back to Scheduled status.
/// Only allowed within 5 minutes of the original check-in timestamp.
/// Staff/Admin only — enforced at the controller level.
/// </summary>
public sealed record RevertArrivalCommand(Guid AppointmentId)
    : IRequest<RevertArrivalConfirmationDto>;

public sealed record RevertArrivalConfirmationDto(
    Guid   AppointmentId,
    string Status,
    string Message);
