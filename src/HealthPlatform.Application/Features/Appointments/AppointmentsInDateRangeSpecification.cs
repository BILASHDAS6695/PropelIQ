using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Returns all appointments for a patient within a date range, ordered by
/// SlotTime ascending. Eagerly loads Provider and Patient navigations.
/// </summary>
internal sealed class AppointmentsInDateRangeSpecification : ISpecification<Appointment>
{
    private readonly Guid           _patientId;
    private readonly DateTimeOffset _from;
    private readonly DateTimeOffset _to;

    public AppointmentsInDateRangeSpecification(
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
        a => a.Patient,
    ];

    public Expression<Func<Appointment, object>>?      OrderBy           => a => a.SlotTime;
    public Expression<Func<Appointment, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
