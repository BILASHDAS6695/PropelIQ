using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

public class AppointmentSlot : BaseEntity
{
    public Guid           ProviderId { get; set; }
    public DateTimeOffset StartTime  { get; set; }
    public DateTimeOffset EndTime    { get; set; }
    public SlotStatus     Status     { get; set; } = SlotStatus.Available;

    public Provider     Provider    { get; set; } = null!;
    public Appointment? Appointment { get; set; }
}
