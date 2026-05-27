using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

public class Appointment : AuditableEntity
{
    public Guid   PatientId      { get; set; }
    public Guid   ProviderId     { get; set; }
    public Guid?  SlotId         { get; set; }     // null for walk-in appointments
    public DateTimeOffset  SlotTime { get; set; }
    public AppointmentStatus Status { get; set; }
    public Guid?  PreferredSlotId { get; set; }
    public bool   IsWalkIn        { get; set; }
    public string? VisitReason    { get; set; }
    public int?   QueuePosition   { get; set; }    // walk-in queue order
    public DateTimeOffset? ArrivalTime { get; set; }  // auto-set at registration

    public PatientProfile   Patient  { get; set; } = null!;
    public Provider         Provider { get; set; } = null!;
    public AppointmentSlot? Slot     { get; set; }     // null for walk-ins
    public IntakeRecord? IntakeRecord { get; set; }
    public PreferredSlotPreference? PreferredSlotPreference { get; set; }
    public ICollection<Notification> Notifications { get; set; } = [];
}
