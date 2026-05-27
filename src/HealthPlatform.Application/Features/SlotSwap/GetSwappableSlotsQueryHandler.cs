using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.SlotSwap;

internal sealed class GetSwappableSlotsQueryHandler
    : IRequestHandler<GetSwappableSlotsQuery, IReadOnlyList<SwappableSlotDto>>
{
    private readonly IUnitOfWork _uow;

    public GetSwappableSlotsQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<SwappableSlotDto>> Handle(
        GetSwappableSlotsQuery query,
        CancellationToken      ct)
    {
        // ── 1. Load requester's appointment ──────────────────────────────
        var requesterAppt = await _uow.Repository<Appointment>()
            .GetByIdAsync(query.RequesterAppointmentId, ct)
            ?? throw new NotFoundException(nameof(Appointment), query.RequesterAppointmentId);

        // ── 2. Find booked appointments: same provider, not requester's ──
        var spec = new SwappableAppointmentsSpecification(
            requesterAppt.ProviderId,
            query.RequesterAppointmentId);

        var candidates = await _uow.Repository<Appointment>().GetAsync(spec, ct);

        // ── 3. Return anonymized DTOs (time only, no patient identity) ───
        return candidates
            .Select(a => new SwappableSlotDto(a.Id, a.SlotTime))
            .OrderBy(d => d.SlotTime)
            .ToList()
            .AsReadOnly();
    }
}
