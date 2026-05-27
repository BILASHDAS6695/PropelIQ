using MediatR;

namespace HealthPlatform.Application.Features.Providers;

public sealed record GetProvidersQuery(string? Specialty = null)
    : IRequest<IReadOnlyList<ProviderDto>>;

public sealed record ProviderDto(
    Guid    ProviderId,
    string  Name,
    string? Specialty);
