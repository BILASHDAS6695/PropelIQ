using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;

namespace HealthPlatform.Application.Features.PdfReport;

internal sealed class PdfReportByIdSpecification
    : ISpecification<Domain.Entities.PdfReport>
{
    private readonly Guid _id;
    public PdfReportByIdSpecification(Guid id) => _id = id;

    public Expression<Func<Domain.Entities.PdfReport, bool>>? Criteria =>
        r => r.Id == _id;

    public List<Expression<Func<Domain.Entities.PdfReport, object>>> Includes => [];
    public Expression<Func<Domain.Entities.PdfReport, object>>? OrderBy            => null;
    public Expression<Func<Domain.Entities.PdfReport, object>>? OrderByDescending  => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
