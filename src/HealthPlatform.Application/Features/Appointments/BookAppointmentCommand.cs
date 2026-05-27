using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

public sealed record BookAppointmentCommand(
    Guid    SlotId,
    string? VisitReason    = null,
    bool    ForceBook      = false,   // patient ack (soft) or staff override (hard)
    string? OverrideReason = null)    // required when ForceBook = true
    : IRequest<BookingConfirmationDto>;

public sealed record BookingConfirmationDto(
    Guid           AppointmentId,
    Guid           ProviderId,
    string         ProviderName,
    DateTimeOffset AppointmentTime,
    string         Status,
    string?        ConflictWarning = null);  // non-null when a soft or override conflict was present
