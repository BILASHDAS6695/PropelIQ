using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Returns all WalkIn appointments for a provider on a given UTC calendar day.
/// Used to determine the next available queue position.
/// </summary>
internal sealed class WalkInQueuePositionSpecification : ISpecification<Appointment>
{
    private readonly Guid           _providerId;
    private readonly DateTimeOffset _dayStart;
    private readonly DateTimeOffset _dayEnd;

    public WalkInQueuePositionSpecification(Guid providerId, DateOnly date)
    {
        _providerId = providerId;
        _dayStart   = new DateTimeOffset(date.Year, date.Month, date.Day, 0,  0,  0, TimeSpan.Zero);
        _dayEnd     = new DateTimeOffset(date.Year, date.Month, date.Day, 23, 59, 59, TimeSpan.Zero);
    }

    public Expression<Func<Appointment, bool>>? Criteria =>
        a => a.ProviderId  == _providerId
          && a.Status      == AppointmentStatus.WalkIn
          && a.ArrivalTime >= _dayStart
          && a.ArrivalTime <= _dayEnd;

    public List<Expression<Func<Appointment, object>>> Includes           => [];
    public Expression<Func<Appointment, object>>?      OrderBy           => null;
    public Expression<Func<Appointment, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
