using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

/// <summary>
/// Append-only audit record. Never modified or deleted after insert.
/// Hash chain guarantees tamper-evidence (HIPAA DR-016).
/// </summary>
public class AuditLog : BaseEntity
{
    /// <summary>Authenticated user who triggered the change. Null for system operations.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Type of data operation performed.</summary>
    public AuditAction Action { get; set; }

    /// <summary>CLR type name of the entity that changed (e.g. "Appointment").</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Primary key of the changed entity.</summary>
    public Guid EntityId { get; set; }

    /// <summary>UTC timestamp of the audit event.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Changed properties serialised as JSONB: { "PropertyName": { "Old": ..., "New": ... } }
    /// </summary>
    public string Details { get; set; } = "{}";

    /// <summary>CurrentHash of the immediately preceding AuditLog row. Null for the first entry.</summary>
    public string? PreviousHash { get; set; }

    /// <summary>
    /// SHA-256( PreviousHash + Timestamp + Action + EntityId + UserId ).
    /// Verified by compliance tooling to detect tampering.
    /// </summary>
    public string CurrentHash { get; set; } = string.Empty;
}
