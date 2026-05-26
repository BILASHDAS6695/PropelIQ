using HealthPlatform.Domain.Common;

namespace HealthPlatform.Domain.Entities;

public class PatientProfile : AuditableEntity
{
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly Dob { get; set; }
    public string? Phone { get; set; }
    public string? InsuranceProviderName { get; set; }
    public string? InsuranceMemberId { get; set; }

    public User User { get; set; } = null!;
    public ICollection<Appointment> Appointments { get; set; } = [];
    public ICollection<ClinicalDocument> ClinicalDocuments { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
    public PatientView360? PatientView360 { get; set; }
}
