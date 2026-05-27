using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Matches <see cref="SlotSwapRequest"/> rows that are still <c>Pending</c>
/// but whose expiry timestamp has passed. Used by the auto-expiry background sweep.
/// </summary>
public sealed class ExpiredPendingSwapRequestsSpecification : ISpecification<SlotSwapRequest>
{
    private readonly DateTimeOffset _now;

    public ExpiredPendingSwapRequestsSpecification(DateTimeOffset now) => _now = now;

    public Expression<Func<SlotSwapRequest, bool>>? Criteria =>
        r => r.Status == SlotSwapStatus.Pending && r.ExpiresAt <= _now;

    public List<Expression<Func<SlotSwapRequest, object>>> Includes { get; } =
    [
        r => r.RequesterPatient,
    ];

    public Expression<Func<SlotSwapRequest, object>>?      OrderBy           => null;
    public Expression<Func<SlotSwapRequest, object>>?      OrderByDescending => null;
    public bool                                            IsPagingEnabled   => false;
    public int                                             Skip              => 0;
    public int                                             Take              => 0;
}
