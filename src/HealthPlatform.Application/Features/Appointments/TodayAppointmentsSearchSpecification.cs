using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Returns today's non-terminal appointments (excluding Cancelled and Completed)
/// with optional narrowing by provider, patient name fragment, or appointment ID.
/// Eagerly loads the Patient navigation for name display and name-based filtering.
/// </summary>
internal sealed class TodayAppointmentsSearchSpecification : ISpecification<Appointment>
{
    private readonly Guid?          _providerId;
    private readonly DateTimeOffset _dayStart;
    private readonly DateTimeOffset _dayEnd;
    private readonly string?        _nameFragment;   // pre-lowercased
    private readonly Guid?          _appointmentId;

    public TodayAppointmentsSearchSpecification(
        Guid?    providerId,
        DateOnly today,
        string?  nameFragment,
        Guid?    appointmentId)
    {
        _providerId    = providerId;
        _dayStart      = new DateTimeOffset(today.Year, today.Month, today.Day, 0,  0,  0, TimeSpan.Zero);
        _dayEnd        = new DateTimeOffset(today.Year, today.Month, today.Day, 23, 59, 59, TimeSpan.Zero);
        _nameFragment  = nameFragment?.Trim().ToLower();
        _appointmentId = appointmentId;
    }

    public Expression<Func<Appointment, bool>>? Criteria =>
        a => a.SlotTime >= _dayStart
          && a.SlotTime <= _dayEnd
          && a.Status != AppointmentStatus.Cancelled
          && a.Status != AppointmentStatus.Completed
          && (_providerId    == null || a.ProviderId == _providerId)
          && (_appointmentId == null || a.Id         == _appointmentId)
          && (_nameFragment  == null
              || a.Patient.FirstName.ToLower().Contains(_nameFragment)
              || a.Patient.LastName.ToLower().Contains(_nameFragment));

    public List<Expression<Func<Appointment, object>>> Includes =>
    [
        a => a.Patient
    ];

    public Expression<Func<Appointment, object>>?      OrderBy           => a => a.SlotTime;
    public Expression<Func<Appointment, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
