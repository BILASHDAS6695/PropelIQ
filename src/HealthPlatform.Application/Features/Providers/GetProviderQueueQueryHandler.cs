using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.Providers;

internal sealed class GetProviderQueueQueryHandler
    : IRequestHandler<GetProviderQueueQuery, IReadOnlyList<QueueEntryDto>>
{
    private readonly IUnitOfWork _uow;

    public GetProviderQueueQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<QueueEntryDto>> Handle(
        GetProviderQueueQuery query,
        CancellationToken     ct)
    {
        var appointments = await _uow.Repository<Appointment>()
            .GetAsync(new ProviderQueueByDateSpecification(query.ProviderId, query.Date), ct);

        return appointments
            .Select(a => new QueueEntryDto(
                a.Id,
                a.PatientId,
                $"{a.Patient.FirstName} {a.Patient.LastName}",
                a.Status.ToString(),
                a.IsWalkIn ? (a.ArrivalTime ?? a.SlotTime) : a.SlotTime,
                a.QueuePosition,
                a.VisitReason,
                a.IsWalkIn,
                a.ArrivalTime,
                a.ArrivalTime.HasValue && a.ArrivalTime.Value > a.SlotTime.AddMinutes(15)))
            .ToList();
    }
}
