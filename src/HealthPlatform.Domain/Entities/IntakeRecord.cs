using System.Text.Json;
using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

public class IntakeRecord : AuditableEntity
{
    public Guid PatientId { get; set; }
    public Guid AppointmentId { get; set; }
    public IntakeMode Mode { get; set; }
    public JsonDocument? DataJson { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public PatientProfile Patient { get; set; } = null!;
    public Appointment Appointment { get; set; } = null!;
}
