using HealthPlatform.Domain.Common;

namespace HealthPlatform.Domain.Entities;

/// <summary>
/// Marks a specific calendar date as unavailable for a provider
/// (vacation, public holiday, personal leave, etc.).
/// Slot generation skips these dates when producing <see cref="AppointmentSlot"/>
/// records.
/// </summary>
public class ProviderUnavailability : AuditableEntity
{
    public Guid     ProviderId      { get; set; }
    public DateOnly UnavailableDate { get; set; }
    public string?  Reason          { get; set; }

    public Provider Provider { get; set; } = null!;
}
