using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Returns all appointments for a provider (or all providers) within a date
/// range, ordered by SlotTime ascending. Used by staff/admin calendar view.
/// Eagerly loads Provider and Patient navigations.
/// </summary>
internal sealed class ProviderAppointmentsInDateRangeSpecification : ISpecification<Appointment>
{
    private readonly Guid?          _providerId;
    private readonly DateTimeOffset _from;
    private readonly DateTimeOffset _to;

    public ProviderAppointmentsInDateRangeSpecification(
        Guid?          providerId,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        _providerId = providerId;
        _from       = from;
        _to         = to;
    }

    public Expression<Func<Appointment, bool>>? Criteria =>
        a => a.SlotTime >= _from
          && a.SlotTime <= _to
          && (_providerId == null || a.ProviderId == _providerId);

    public List<Expression<Func<Appointment, object>>> Includes =>
    [
        a => a.Provider,
        a => a.Patient,
    ];

    public Expression<Func<Appointment, object>>?      OrderBy           => a => a.SlotTime;
    public Expression<Func<Appointment, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
