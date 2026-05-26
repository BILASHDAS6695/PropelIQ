using HealthPlatform.Domain.Common;

namespace HealthPlatform.Domain.Entities;

public class Provider : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Specialty { get; set; }
    public Guid? ScheduleTemplateId { get; set; }

    public ICollection<AppointmentSlot> AppointmentSlots { get; set; } = [];
    public ICollection<Appointment> Appointments { get; set; } = [];
}
