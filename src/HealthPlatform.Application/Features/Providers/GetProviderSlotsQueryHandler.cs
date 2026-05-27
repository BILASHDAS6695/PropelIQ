using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.Providers;

internal sealed class GetProviderSlotsQueryHandler
    : IRequestHandler<GetProviderSlotsQuery, IReadOnlyList<SlotDto>>
{
    private readonly IUnitOfWork _uow;

    public GetProviderSlotsQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<SlotDto>> Handle(
        GetProviderSlotsQuery query,
        CancellationToken     ct)
    {
        var from = new DateTimeOffset(
            query.Date.Year, query.Date.Month, query.Date.Day,
            0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(1);

        var slots = await _uow.Repository<AppointmentSlot>()
            .GetAsync(new SlotsByProviderAndDateSpecification(query.ProviderId, from, to), ct);

        return slots
            .Select(s => new SlotDto(s.Id, s.ProviderId, s.StartTime, s.EndTime, s.Status.ToString()))
            .ToList();
    }
}
