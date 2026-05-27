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

    // ── Account lockout ───────────────────────────────────────────────────
    /// <summary>Number of consecutive failed login attempts since last success.</summary>
    public int FailedLoginAttempts { get; set; }

    /// <summary>
    /// UTC timestamp at which the account lockout expires.
    /// <c>null</c> means the account is not currently locked.
    /// </summary>
    public DateTimeOffset? LockoutEnd { get; set; }

    // ── Password policy ───────────────────────────────────────────────────
    /// <summary>
    /// UTC timestamp when the current password expires and must be changed.
    /// Set to <c>UtcNow + 90 days</c> on every successful password change.
    /// </summary>
    public DateTimeOffset? CredentialExpiresAt { get; set; }

    /// <summary>
    /// Ordered list of the last N bcrypt hashes (most recent first).
    /// Populated and trimmed by <c>ChangePasswordCommandHandler</c>.
    /// Stored as a JSON array in the database column <c>password_history</c>.
    /// </summary>
    public List<string> PasswordHistory { get; set; } = [];

    public PatientProfile? PatientProfile { get; set; }
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
}
