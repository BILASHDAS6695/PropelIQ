using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;
using HealthPlatform.Domain.ValueObjects;

namespace HealthPlatform.Domain.Entities;

public class IntakeRecord : AuditableEntity
{
    public Guid PatientId { get; set; }
    public Guid AppointmentId { get; set; }
    public IntakeMode Mode { get; set; }
    public IntakeStatus Status { get; set; } = IntakeStatus.Draft;
    public IntakeData? Data { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedByProviderId { get; set; }

    // Navigation
    public PatientProfile Patient { get; set; } = null!;
    public Appointment Appointment { get; set; } = null!;
}
