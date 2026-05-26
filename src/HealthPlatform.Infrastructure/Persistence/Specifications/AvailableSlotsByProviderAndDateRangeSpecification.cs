using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Infrastructure.Persistence.Specifications;

/// <summary>
/// Returns available appointment slots for a given provider within a
/// date/time window, ordered by start time ascending.
/// Usage: new AvailableSlotsByProviderAndDateRangeSpecification(providerId, from, to)
/// </summary>
public sealed class AvailableSlotsByProviderAndDateRangeSpecification
    : BaseSpecification<AppointmentSlot>
{
    public AvailableSlotsByProviderAndDateRangeSpecification(
        Guid           providerId,
        DateTimeOffset from,
        DateTimeOffset to)
        : base(s => s.ProviderId == providerId
                 && s.IsAvailable
                 && s.StartTime >= from
                 && s.StartTime < to)
    {
        ApplyOrderBy(s => s.StartTime);
    }
}
