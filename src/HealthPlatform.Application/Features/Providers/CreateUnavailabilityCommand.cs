using MediatR;

namespace HealthPlatform.Application.Features.Providers;

public sealed record CreateUnavailabilityCommand(
    Guid     ProviderId,
    DateOnly UnavailableDate,
    string?  Reason = null) : IRequest<Guid>;
