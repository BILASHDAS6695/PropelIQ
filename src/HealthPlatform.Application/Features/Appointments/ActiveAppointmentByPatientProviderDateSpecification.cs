using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Returns active (Scheduled or Booked) appointments for a patient with a
/// specific provider on a given UTC calendar day.
/// Used to enforce the one-active-appointment-per-provider-per-day rule.
/// </summary>
internal sealed class ActiveAppointmentByPatientProviderDateSpecification
    : ISpecification<Appointment>
{
    private readonly Guid           _patientId;
    private readonly Guid           _providerId;
    private readonly DateTimeOffset _dayStart;
    private readonly DateTimeOffset _dayEnd;

    public ActiveAppointmentByPatientProviderDateSpecification(
        Guid     patientId,
        Guid     providerId,
        DateOnly date)
    {
        _patientId  = patientId;
        _providerId = providerId;
        _dayStart   = new DateTimeOffset(date.Year, date.Month, date.Day, 0,  0,  0,  TimeSpan.Zero);
        _dayEnd     = new DateTimeOffset(date.Year, date.Month, date.Day, 23, 59, 59, TimeSpan.Zero);
    }

    public Expression<Func<Appointment, bool>>? Criteria =>
        a => a.PatientId  == _patientId
          && a.ProviderId == _providerId
          && a.SlotTime   >= _dayStart
          && a.SlotTime   <= _dayEnd
          && (a.Status == AppointmentStatus.Scheduled
           || a.Status == AppointmentStatus.Booked);

    public List<Expression<Func<Appointment, object>>> Includes           => [];
    public Expression<Func<Appointment, object>>?      OrderBy           => null;
    public Expression<Func<Appointment, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
