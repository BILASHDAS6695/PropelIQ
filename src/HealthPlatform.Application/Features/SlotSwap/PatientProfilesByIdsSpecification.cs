using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Loads <see cref="PatientProfile"/> rows whose <c>Id</c> is in the provided set.
/// Used by <see cref="GetPendingSwapRequestsQueryHandler"/> to batch-fetch target
/// patient profiles in a single round-trip.
/// </summary>
internal sealed class PatientProfilesByIdsSpecification : ISpecification<PatientProfile>
{
    private readonly HashSet<Guid> _ids;

    public PatientProfilesByIdsSpecification(HashSet<Guid> ids) => _ids = ids;

    public Expression<Func<PatientProfile, bool>>? Criteria =>
        p => _ids.Contains(p.Id);

    public List<Expression<Func<PatientProfile, object>>> Includes           => [];
    public Expression<Func<PatientProfile, object>>?      OrderBy           => null;
    public Expression<Func<PatientProfile, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
