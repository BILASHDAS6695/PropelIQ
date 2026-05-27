using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

public sealed record BookAppointmentCommand(
    Guid    SlotId,
    string? VisitReason = null) : IRequest<BookingConfirmationDto>;

public sealed record BookingConfirmationDto(
    Guid           AppointmentId,
    Guid           ProviderId,
    string         ProviderName,
    DateTimeOffset AppointmentTime,
    string         Status);
