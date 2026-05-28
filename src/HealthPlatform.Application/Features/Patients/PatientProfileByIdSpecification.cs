using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Patients;

/// <summary>Loads a PatientProfile by its primary key.</summary>
internal sealed class PatientProfileByIdSpecification : ISpecification<PatientProfile>
{
    private readonly Guid _id;
    public PatientProfileByIdSpecification(Guid id) => _id = id;

    public Expression<Func<PatientProfile, bool>>? Criteria => p => p.Id == _id;
    public List<Expression<Func<PatientProfile, object>>> Includes => [];
    public Expression<Func<PatientProfile, object>>? OrderBy            => null;
    public Expression<Func<PatientProfile, object>>? OrderByDescending  => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
