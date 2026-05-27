using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.Providers;

/// <summary>
/// Returns all active appointments for a provider on a given UTC calendar day:
/// Scheduled (online bookings), Booked, and WalkIn (walk-ins).
/// Ordered by SlotTime ascending for display in the provider's daily queue.
/// </summary>
internal sealed class ProviderQueueByDateSpecification : ISpecification<Appointment>
{
    private readonly Guid           _providerId;
    private readonly DateTimeOffset _dayStart;
    private readonly DateTimeOffset _dayEnd;

    public ProviderQueueByDateSpecification(Guid providerId, DateOnly date)
    {
        _providerId = providerId;
        _dayStart   = new DateTimeOffset(date.Year, date.Month, date.Day, 0,  0,  0, TimeSpan.Zero);
        _dayEnd     = new DateTimeOffset(date.Year, date.Month, date.Day, 23, 59, 59, TimeSpan.Zero);
    }

    public Expression<Func<Appointment, bool>>? Criteria =>
        a => a.ProviderId == _providerId
          && (a.Status == AppointmentStatus.Scheduled
           || a.Status == AppointmentStatus.WalkIn
           || a.Status == AppointmentStatus.Booked
           || a.Status == AppointmentStatus.Arrived
           || a.Status == AppointmentStatus.InProgress)
          && a.SlotTime >= _dayStart
          && a.SlotTime <= _dayEnd;

    public List<Expression<Func<Appointment, object>>> Includes => [a => a.Patient];
    public Expression<Func<Appointment, object>>?      OrderBy           => a => a.SlotTime;
    public Expression<Func<Appointment, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
