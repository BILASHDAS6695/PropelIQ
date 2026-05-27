using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Returns a PatientProfile by its own primary key (Id).
/// Distinct from PatientProfileByUserIdSpecification which queries by UserId.
/// </summary>
internal sealed class PatientProfileByPatientIdSpecification : ISpecification<PatientProfile>
{
    private readonly Guid _patientId;

    public PatientProfileByPatientIdSpecification(Guid patientId) => _patientId = patientId;

    public Expression<Func<PatientProfile, bool>>? Criteria =>
        p => p.Id == _patientId;

    public List<Expression<Func<PatientProfile, object>>> Includes           => [];
    public Expression<Func<PatientProfile, object>>?      OrderBy           => null;
    public Expression<Func<PatientProfile, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
