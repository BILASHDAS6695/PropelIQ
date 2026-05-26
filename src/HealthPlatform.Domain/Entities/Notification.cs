using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

public class Notification : BaseEntity
{
    public Guid PatientId { get; set; }
    public Guid? AppointmentId { get; set; }
    public NotificationChannel Channel { get; set; }
    public NotificationType Type { get; set; }
    public DateTimeOffset SentAt { get; set; }
    public DeliveryStatus DeliveryStatus { get; set; }

    public PatientProfile Patient { get; set; } = null!;
    public Appointment? Appointment { get; set; }
}
