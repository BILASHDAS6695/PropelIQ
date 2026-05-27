using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Returns all Scheduled or Booked appointments whose slot started at least
/// <paramref name="cutoffUtc"/> ago and whose patient never checked in
/// (ArrivalTime is null).  Used by the Hangfire auto-mark job to find
/// appointments eligible for automatic no-show marking.
///
/// The caller passes <c>DateTimeOffset.UtcNow.AddMinutes(-30)</c> as
/// <paramref name="cutoffUtc"/>, selecting only appointments whose
/// <see cref="Appointment.SlotTime"/> is ≥ 30 min in the past.
///
/// Eagerly loads Slot and Patient (for slot freeing, counter increment,
/// and follow-up email).
/// </summary>
public sealed class ActiveUnattendedPastCutoffSpecification : ISpecification<Appointment>
{
    private readonly DateTimeOffset _cutoffUtc;

    public ActiveUnattendedPastCutoffSpecification(DateTimeOffset cutoffUtc)
        => _cutoffUtc = cutoffUtc;

    public Expression<Func<Appointment, bool>>? Criteria =>
        a => (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Booked)
          && a.ArrivalTime == null
          && a.SlotTime <= _cutoffUtc;

    public List<Expression<Func<Appointment, object>>> Includes =>
    [
        a => a.Slot!,
        a => a.Patient,
    ];

    public Expression<Func<Appointment, object>>? OrderBy           => null;
    public Expression<Func<Appointment, object>>? OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
