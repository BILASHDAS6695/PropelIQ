using MediatR;

namespace HealthPlatform.Application.Features.Providers;

public sealed record GetProviderSlotsQuery(Guid ProviderId, DateOnly Date)
    : IRequest<IReadOnlyList<SlotDto>>;

public sealed record SlotDto(
    Guid           SlotId,
    Guid           ProviderId,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string         Status);
