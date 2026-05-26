using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

public class PreferredSlotPreference : BaseEntity
{
    public Guid AppointmentId { get; set; }
    public Guid PreferredSlotId { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
    public PreferredSlotStatus Status { get; set; }

    public Appointment Appointment { get; set; } = null!;
}
