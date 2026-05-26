using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

public class User : AuditableEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    public PatientProfile? PatientProfile { get; set; }
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
}
