using HealthPlatform.Domain.Common;

namespace HealthPlatform.Domain.Entities;

/// <summary>
/// Defines a recurring weekly availability window for a provider.
/// Slot generation reads these rules to produce <see cref="AppointmentSlot"/>
/// records for the next 90 days.
/// </summary>
public class ProviderScheduleRule : AuditableEntity
{
    public Guid      ProviderId          { get; set; }
    public DayOfWeek DayOfWeek           { get; set; }
    public TimeOnly  StartTime           { get; set; }
    public TimeOnly  EndTime             { get; set; }
    public int       SlotDurationMinutes { get; set; } = 30;

    public Provider Provider { get; set; } = null!;
}
