using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.PdfReport;

/// <summary>
/// Returns non-expired, non-failed PDF reports for the same patient and date
/// range. Used for deduplication of concurrent report requests.
/// </summary>
internal sealed class ExistingValidPdfReportSpecification
    : ISpecification<Domain.Entities.PdfReport>
{
    private readonly Guid           _patientId;
    private readonly DateTimeOffset _from;
    private readonly DateTimeOffset _to;
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public ExistingValidPdfReportSpecification(
        Guid           patientId,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        _patientId = patientId;
        _from      = from;
        _to        = to;
    }

    public Expression<Func<Domain.Entities.PdfReport, bool>>? Criteria =>
        r => r.PatientId == _patientId
          && r.DateFrom   == _from
          && r.DateTo     == _to
          && r.ExpiresAt  >  _now
          && r.Status     != PdfReportStatus.Failed;

    public List<Expression<Func<Domain.Entities.PdfReport, object>>> Includes => [];
    public Expression<Func<Domain.Entities.PdfReport, object>>? OrderBy            => null;
    public Expression<Func<Domain.Entities.PdfReport, object>>? OrderByDescending  =>
        r => r.CreatedAt;
    public bool IsPagingEnabled => true;
    public int  Skip            => 0;
    public int  Take            => 1;
}
