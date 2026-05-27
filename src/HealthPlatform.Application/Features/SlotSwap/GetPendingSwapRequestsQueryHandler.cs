using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.SlotSwap;

internal sealed class GetPendingSwapRequestsQueryHandler
    : IRequestHandler<GetPendingSwapRequestsQuery, IReadOnlyList<PendingSwapRequestSummaryDto>>
{
    private readonly IUnitOfWork _uow;

    public GetPendingSwapRequestsQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<PendingSwapRequestSummaryDto>> Handle(
        GetPendingSwapRequestsQuery request,
        CancellationToken           ct)
    {
        // ── 1. Load all pending swap requests (requester patient + both appts) ──
        var pending = await _uow.Repository<SlotSwapRequest>()
            .GetAsync(new PendingSwapRequestsSpecification(), ct);

        if (pending.Count == 0)
            return [];

        // ── 2. Collect distinct target patient IDs for batch fetch ─────────
        var targetPatientIds = pending
            .Select(r => r.TargetAppointment.PatientId)
            .Distinct()
            .ToHashSet();

        // ── 3. Load target patient profiles in one round-trip ──────────────
        var targetProfiles = await _uow.Repository<PatientProfile>()
            .GetAsync(new PatientProfilesByIdsSpecification(targetPatientIds), ct);

        var targetProfileMap = targetProfiles.ToDictionary(p => p.Id);

        // ── 4. Project to staff-visible DTOs ──────────────────────────────
        var result = new List<PendingSwapRequestSummaryDto>(pending.Count);

        foreach (var r in pending)
        {
            var requester = r.RequesterPatient;
            targetProfileMap.TryGetValue(r.TargetAppointment.PatientId, out var target);

            result.Add(new PendingSwapRequestSummaryDto(
                SwapRequestId:      r.Id,
                RequesterPatientId: requester.Id,
                RequesterFullName:  $"{requester.FirstName} {requester.LastName}",
                RequesterSlotTime:  r.RequesterAppointment.SlotTime,
                TargetPatientId:    r.TargetAppointment.PatientId,
                TargetFullName:     target is not null
                                        ? $"{target.FirstName} {target.LastName}"
                                        : "Unknown",
                TargetSlotTime:     r.TargetAppointment.SlotTime,
                ExpiresAt:          r.ExpiresAt));
        }

        return result;
    }
}
