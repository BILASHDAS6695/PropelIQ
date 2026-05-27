using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Loads a <see cref="SlotSwapRequest"/> by ID, eagerly including both
/// appointment navigations and the requester's patient profile so that
/// the handler can access slot times and patient IDs in one trip.
/// </summary>
internal sealed class SwapRequestWithAppointmentsSpecification : ISpecification<SlotSwapRequest>
{
    private readonly Guid _swapRequestId;

    public SwapRequestWithAppointmentsSpecification(Guid swapRequestId) =>
        _swapRequestId = swapRequestId;

    public Expression<Func<SlotSwapRequest, bool>>? Criteria =>
        r => r.Id == _swapRequestId;

    public List<Expression<Func<SlotSwapRequest, object>>> Includes { get; } =
    [
        r => r.RequesterAppointment,
        r => r.TargetAppointment,
        r => r.RequesterPatient,
    ];

    public Expression<Func<SlotSwapRequest, object>>?      OrderBy           => null;
    public Expression<Func<SlotSwapRequest, object>>?      OrderByDescending => null;
    public bool                                            IsPagingEnabled   => false;
    public int                                             Skip              => 0;
    public int                                             Take              => 0;
}
