using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Returns appointments for a patient within a date range, ordered by
/// SlotTime ascending, capped at 100 rows for report generation.
/// Eagerly loads the Provider navigation.
/// </summary>
internal sealed class AppointmentsForReportSpecification : ISpecification<Appointment>
{
    private readonly Guid           _patientId;
    private readonly DateTimeOffset _from;
    private readonly DateTimeOffset _to;

    public AppointmentsForReportSpecification(
        Guid           patientId,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        _patientId = patientId;
        _from      = from;
        _to        = to;
    }

    public Expression<Func<Appointment, bool>>? Criteria =>
        a => a.PatientId == _patientId
          && a.SlotTime  >= _from
          && a.SlotTime  <= _to;

    public List<Expression<Func<Appointment, object>>> Includes =>
    [
        a => a.Provider,
    ];

    public Expression<Func<Appointment, object>>?      OrderBy           => a => a.SlotTime;
    public Expression<Func<Appointment, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => true;
    public int  Skip            => 0;
    public int  Take            => 100;   // hard cap per AC (max 100 per run)
}
