using HealthPlatform.Domain.Common;

namespace HealthPlatform.Domain.Entities;

public class AppointmentSlot : BaseEntity
{
    public Guid ProviderId { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public bool IsAvailable { get; set; }

    public Provider Provider { get; set; } = null!;
    public Appointment? Appointment { get; set; }
}
