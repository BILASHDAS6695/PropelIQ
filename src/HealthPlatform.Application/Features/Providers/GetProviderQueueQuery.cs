using MediatR;

namespace HealthPlatform.Application.Features.Providers;

public sealed record GetProviderQueueQuery(Guid ProviderId, DateOnly Date)
    : IRequest<IReadOnlyList<QueueEntryDto>>;

public sealed record QueueEntryDto(
    Guid           AppointmentId,
    Guid           PatientId,
    string         Status,
    DateTimeOffset AppointmentTime,
    int?           QueuePosition,
    string?        VisitReason,
    bool           IsWalkIn);
