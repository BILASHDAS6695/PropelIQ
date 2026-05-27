using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Fetches all appointments in a date range that are either Completed or
/// NoShow (the denominator + numerator for the no-show rate).  Eagerly
/// loads the Provider navigation for grouping by provider name.
/// </summary>
internal sealed class NoShowReportSpecification : ISpecification<Appointment>
{
    private readonly DateTimeOffset _from;
    private readonly DateTimeOffset _to;
    private readonly Guid?          _providerId;

    public NoShowReportSpecification(DateOnly dateFrom, DateOnly dateTo, Guid? providerId)
    {
        _from       = new DateTimeOffset(dateFrom.Year, dateFrom.Month, dateFrom.Day,  0,  0,  0, TimeSpan.Zero);
        _to         = new DateTimeOffset(dateTo.Year,   dateTo.Month,   dateTo.Day,   23, 59, 59, TimeSpan.Zero);
        _providerId = providerId;
    }

    public Expression<Func<Appointment, bool>>? Criteria =>
        a => a.SlotTime >= _from
          && a.SlotTime <= _to
          && (a.Status == AppointmentStatus.Completed || a.Status == AppointmentStatus.NoShow)
          && (_providerId == null || a.ProviderId == _providerId.Value);

    public List<Expression<Func<Appointment, object>>> Includes =>
    [
        a => a.Provider,
    ];

    public Expression<Func<Appointment, object>>? OrderBy           => null;
    public Expression<Func<Appointment, object>>? OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
