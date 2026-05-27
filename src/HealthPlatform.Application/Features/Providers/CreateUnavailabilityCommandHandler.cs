using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.Providers;

internal sealed class CreateUnavailabilityCommandHandler
    : IRequestHandler<CreateUnavailabilityCommand, Guid>
{
    private readonly IUnitOfWork _uow;

    public CreateUnavailabilityCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Guid> Handle(CreateUnavailabilityCommand request, CancellationToken ct)
    {
        var entry = new ProviderUnavailability
        {
            Id              = Guid.NewGuid(),
            ProviderId      = request.ProviderId,
            UnavailableDate = request.UnavailableDate,
            Reason          = request.Reason
        };

        await _uow.Repository<ProviderUnavailability>().AddAsync(entry, ct);
        await _uow.SaveChangesAsync(ct);
        return entry.Id;
    }
}
