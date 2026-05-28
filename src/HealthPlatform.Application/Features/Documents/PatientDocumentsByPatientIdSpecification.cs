using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Documents;

internal sealed class PatientDocumentsByPatientIdSpecification : ISpecification<ClinicalDocument>
{
    private readonly Guid _patientId;

    public PatientDocumentsByPatientIdSpecification(Guid patientId)
        => _patientId = patientId;

    public Expression<Func<ClinicalDocument, bool>>? Criteria
        => d => d.PatientId == _patientId && !d.IsDeleted;

    public List<Expression<Func<ClinicalDocument, object>>> Includes { get; } = [];
    public Expression<Func<ClinicalDocument, object>>? OrderBy           => null;
    public Expression<Func<ClinicalDocument, object>>? OrderByDescending => d => d.UploadedAt;
    public bool                                        IsPagingEnabled   => false;
    public int                                         Skip              => 0;
    public int                                         Take              => 0;
}
