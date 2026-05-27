using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.Providers;

/// <summary>
/// Returns all Available slots for a provider within a UTC date window,
/// ordered by start time ascending. Booked and Blocked slots are excluded.
/// </summary>
internal sealed class SlotsByProviderAndDateSpecification
    : ISpecification<AppointmentSlot>
{
    private readonly Guid           _providerId;
    private readonly DateTimeOffset _from;
    private readonly DateTimeOffset _to;

    public SlotsByProviderAndDateSpecification(
        Guid           providerId,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        _providerId = providerId;
        _from       = from;
        _to         = to;
    }

    public Expression<Func<AppointmentSlot, bool>>? Criteria =>
        s => s.ProviderId == _providerId
          && s.Status     == SlotStatus.Available
          && s.StartTime  >= _from
          && s.StartTime  <  _to;

    public List<Expression<Func<AppointmentSlot, object>>> Includes => [];
    public Expression<Func<AppointmentSlot, object>>?      OrderBy           => s => s.StartTime;
    public Expression<Func<AppointmentSlot, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
