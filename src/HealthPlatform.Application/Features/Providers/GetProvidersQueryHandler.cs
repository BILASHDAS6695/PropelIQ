using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.Providers;

internal sealed class GetProvidersQueryHandler
    : IRequestHandler<GetProvidersQuery, IReadOnlyList<ProviderDto>>
{
    private readonly IUnitOfWork _uow;

    public GetProvidersQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<ProviderDto>> Handle(
        GetProvidersQuery query,
        CancellationToken ct)
    {
        var providers = await _uow.Repository<Provider>()
            .GetAsync(new ProvidersBySpecialtySpecification(query.Specialty), ct);

        return providers
            .Select(p => new ProviderDto(p.Id, p.Name, p.Specialty))
            .ToList();
    }
}
