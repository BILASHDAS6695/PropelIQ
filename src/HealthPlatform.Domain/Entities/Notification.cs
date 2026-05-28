using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

public class Notification : BaseEntity
{
    // ── Recipient ──────────────────────────────────────────────────
    /// <summary>The user who should receive this notification.</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Patient context — null for staff-targeted notifications
    /// (arrival alerts, conflict overrides, etc.).
    /// </summary>
    public Guid? PatientId { get; set; }

    /// <summary>Related appointment — null for non-appointment notifications.</summary>
    public Guid? AppointmentId { get; set; }

    // ── Content ──────────────────────────────────────────────────
    public NotificationChannel Channel { get; set; }
    public NotificationType    Type    { get; set; }
    public string              Title   { get; set; } = string.Empty;
    public string              Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional deep-link route for the Angular SPA, e.g.
    /// <c>/appointments/abc123</c>.
    /// </summary>
    public string? ActionUrl { get; set; }

    // ── Delivery ──────────────────────────────────────────────────
    public DeliveryStatus DeliveryStatus { get; set; }
    public DateTimeOffset SentAt         { get; set; }

    // ── Read state (in-app only) ───────────────────────────────────
    public bool            IsRead { get; set; }
    public DateTimeOffset? ReadAt { get; set; }

    // ── Expiry ───────────────────────────────────────────────────
    /// <summary>
    /// UTC expiry for the notification record. Defaults to 90 days after
    /// <see cref="SentAt"/>. Used by the cleanup job.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    // ── Navigation ──────────────────────────────────────────────────
    public User            User        { get; set; } = null!;
    public PatientProfile? Patient     { get; set; }
    public Appointment?    Appointment { get; set; }
}
