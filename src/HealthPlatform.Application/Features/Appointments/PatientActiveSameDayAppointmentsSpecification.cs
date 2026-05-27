using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Returns all non-terminal appointments for a patient on a given UTC calendar
/// day across ANY provider.  Used for cross-provider conflict detection.
///
/// Non-terminal statuses included: Scheduled, Booked, Arrived, InProgress.
/// Terminal statuses excluded:  Cancelled, NoShow, Completed.
///
/// Eagerly loads the Provider navigation so callers can surface provider names
/// without a second query.
///
/// The optional <paramref name="excludeAppointmentId"/> allows the rescheduling
/// flow to exclude the appointment being rescheduled (self-exclusion).
/// </summary>
internal sealed class PatientActiveSameDayAppointmentsSpecification : ISpecification<Appointment>
{
    private readonly Guid           _patientId;
    private readonly DateTimeOffset _dayStart;
    private readonly DateTimeOffset _dayEnd;
    private readonly Guid?          _excludeAppointmentId;

    public PatientActiveSameDayAppointmentsSpecification(
        Guid     patientId,
        DateOnly date,
        Guid?    excludeAppointmentId = null)
    {
        _patientId            = patientId;
        _excludeAppointmentId = excludeAppointmentId;
        _dayStart = new DateTimeOffset(date.Year, date.Month, date.Day,  0,  0,  0, TimeSpan.Zero);
        _dayEnd   = new DateTimeOffset(date.Year, date.Month, date.Day, 23, 59, 59, TimeSpan.Zero);
    }

    public Expression<Func<Appointment, bool>>? Criteria =>
        a => a.PatientId == _patientId
          && a.SlotTime  >= _dayStart
          && a.SlotTime  <= _dayEnd
          && (a.Status == AppointmentStatus.Scheduled
           || a.Status == AppointmentStatus.Booked
           || a.Status == AppointmentStatus.Arrived
           || a.Status == AppointmentStatus.InProgress)
          && (_excludeAppointmentId == null || a.Id != _excludeAppointmentId.Value);

    public List<Expression<Func<Appointment, object>>> Includes =>
    [
        a => a.Provider   // needed so handlers can return ConflictingProviderName
    ];

    public Expression<Func<Appointment, object>>?      OrderBy           => a => a.SlotTime;
    public Expression<Func<Appointment, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
