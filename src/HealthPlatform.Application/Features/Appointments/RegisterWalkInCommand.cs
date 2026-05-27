using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

public sealed record RegisterWalkInCommand(
    Guid    PatientId,
    Guid    ProviderId,
    string? VisitReason = null) : IRequest<WalkInConfirmationDto>;

public sealed record WalkInConfirmationDto(
    Guid           AppointmentId,
    Guid           PatientId,
    Guid           ProviderId,
    string         ProviderName,
    int            QueuePosition,
    DateTimeOffset ArrivalTime,
    string         Status);
