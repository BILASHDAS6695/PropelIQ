using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;

namespace HealthPlatform.Application.Features.PdfReport;

internal sealed class PdfReportByTokenSpecification
    : ISpecification<Domain.Entities.PdfReport>
{
    private readonly Guid _patientId;
    private readonly Guid _token;

    public PdfReportByTokenSpecification(Guid patientId, Guid token)
    {
        _patientId = patientId;
        _token     = token;
    }

    public Expression<Func<Domain.Entities.PdfReport, bool>>? Criteria =>
        r => r.PatientId == _patientId && r.Token == _token;

    public List<Expression<Func<Domain.Entities.PdfReport, object>>> Includes => [];
    public Expression<Func<Domain.Entities.PdfReport, object>>? OrderBy            => null;
    public Expression<Func<Domain.Entities.PdfReport, object>>? OrderByDescending  => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
