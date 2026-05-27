using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Matches pending swap requests where the given appointment is the requester side.
/// Used to enforce the one-active-swap-per-appointment business rule.
/// </summary>
internal sealed class ActiveSwapRequestByAppointmentSpecification : ISpecification<SlotSwapRequest>
{
    private readonly Guid _requesterAppointmentId;

    public ActiveSwapRequestByAppointmentSpecification(Guid requesterAppointmentId)
        => _requesterAppointmentId = requesterAppointmentId;

    public Expression<Func<SlotSwapRequest, bool>>? Criteria =>
        r => r.RequesterAppointmentId == _requesterAppointmentId
          && r.Status == SlotSwapStatus.Pending;

    public List<Expression<Func<SlotSwapRequest, object>>> Includes { get; } = [];
    public Expression<Func<SlotSwapRequest, object>>?      OrderBy           => null;
    public Expression<Func<SlotSwapRequest, object>>?      OrderByDescending => null;
    public bool                                            IsPagingEnabled   => false;
    public int                                             Skip              => 0;
    public int                                             Take              => 0;
}
