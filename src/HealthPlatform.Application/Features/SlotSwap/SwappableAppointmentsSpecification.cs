using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Matches booked (Scheduled) appointments for a given provider, excluding
/// the requester's own appointment.
/// Walk-in appointments (IsWalkIn = true) are excluded — they have no fixed slot.
/// </summary>
internal sealed class SwappableAppointmentsSpecification : ISpecification<Appointment>
{
    private readonly Guid _providerId;
    private readonly Guid _excludeAppointmentId;

    public SwappableAppointmentsSpecification(Guid providerId, Guid excludeAppointmentId)
    {
        _providerId           = providerId;
        _excludeAppointmentId = excludeAppointmentId;
    }

    public Expression<Func<Appointment, bool>>? Criteria =>
        a => a.ProviderId == _providerId
          && a.Id != _excludeAppointmentId
          && !a.IsWalkIn
          && a.Status == AppointmentStatus.Scheduled;

    public List<Expression<Func<Appointment, object>>> Includes { get; } = [];
    public Expression<Func<Appointment, object>>?      OrderBy           => null;
    public Expression<Func<Appointment, object>>?      OrderByDescending => null;
    public bool                                        IsPagingEnabled   => false;
    public int                                         Skip              => 0;
    public int                                         Take              => 0;
}
