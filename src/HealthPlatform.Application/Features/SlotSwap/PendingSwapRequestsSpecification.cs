using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Loads all <see cref="SlotSwapRequest"/> rows in <c>Pending</c> status,
/// eagerly including both appointment navigations and the requester's patient profile.
/// The target patient profile is fetched separately by the handler because the
/// repository spec infrastructure does not support nested (ThenInclude) paths.
/// </summary>
internal sealed class PendingSwapRequestsSpecification : ISpecification<SlotSwapRequest>
{
    public Expression<Func<SlotSwapRequest, bool>>? Criteria =>
        r => r.Status == SlotSwapStatus.Pending;

    public List<Expression<Func<SlotSwapRequest, object>>> Includes { get; } =
    [
        r => r.RequesterPatient,
        r => r.RequesterAppointment,
        r => r.TargetAppointment,
    ];

    public Expression<Func<SlotSwapRequest, object>>?      OrderBy           =>
        r => r.ExpiresAt;
    public Expression<Func<SlotSwapRequest, object>>?      OrderByDescending => null;
    public bool                                            IsPagingEnabled   => false;
    public int                                             Skip              => 0;
    public int                                             Take              => 0;
}
