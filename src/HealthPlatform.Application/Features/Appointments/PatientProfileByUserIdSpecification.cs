using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Appointments;

internal sealed class PatientProfileByUserIdSpecification : ISpecification<PatientProfile>
{
    private readonly Guid _userId;

    public PatientProfileByUserIdSpecification(Guid userId) => _userId = userId;

    public Expression<Func<PatientProfile, bool>>? Criteria =>
        p => p.UserId == _userId;

    public List<Expression<Func<PatientProfile, object>>> Includes           => [];
    public Expression<Func<PatientProfile, object>>?      OrderBy           => null;
    public Expression<Func<PatientProfile, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
