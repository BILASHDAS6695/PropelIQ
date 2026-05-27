using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

/// <summary>
/// Represents a patient's request to swap their appointment slot with another
/// patient's booked slot at the same provider.
///
/// Privacy rule: the requester never sees the target patient's identity —
/// only the target slot time is exposed.
/// </summary>
public class SlotSwapRequest : AuditableEntity
{
    /// <summary>Patient profile ID of the patient who initiated the swap.</summary>
    public Guid RequesterPatientId { get; set; }

    /// <summary>The requester's current appointment (the slot they are offering).</summary>
    public Guid RequesterAppointmentId { get; set; }

    /// <summary>The target appointment the requester wants to acquire.</summary>
    public Guid TargetAppointmentId { get; set; }

    /// <summary>Current status of the swap request.</summary>
    public SlotSwapStatus Status { get; set; } = SlotSwapStatus.Pending;

    /// <summary>UTC timestamp when the request auto-expires (creation + 24 h).</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Optional reason recorded when the request is cancelled or declined.</summary>
    public string? CancellationReason { get; set; }

    // ── Staff mediation fields (US-030) ───────────────────────────────────

    /// <summary>
    /// Mandatory reason text supplied by staff for any override action
    /// (force-approve, force-decline, or three-way reassignment).
    /// Null for patient-driven outcomes.
    /// </summary>
    public string? OverrideReason { get; set; }

    /// <summary>
    /// User ID of the staff member who performed the override.
    /// Null for patient-driven outcomes.
    /// </summary>
    public Guid? MediatedByUserId { get; set; }

    /// <summary>
    /// UTC timestamp when the staff override was applied.
    /// Null for patient-driven outcomes.
    /// </summary>
    public DateTimeOffset? OverriddenAt { get; set; }

    /// <summary>
    /// For three-way reassignment only: the new <see cref="AppointmentSlot"/> ID
    /// assigned to the target patient after the requester takes the target's
    /// original slot. Null for all other swap outcomes.
    /// </summary>
    public Guid? ThreeWayNewTargetSlotId { get; set; }

    // ── Optimistic concurrency token (US-030 edge case) ───────────────────

    /// <summary>
    /// PostgreSQL <c>xmin</c>-backed concurrency token. Automatically incremented
    /// by the database on every row update. Prevents two staff members from
    /// mediating the same swap request simultaneously.
    /// </summary>
    public uint Version { get; set; }

    // ── Navigation properties ──────────────────────────────────────────────
    public PatientProfile RequesterPatient     { get; set; } = null!;
    public Appointment    RequesterAppointment { get; set; } = null!;
    public Appointment    TargetAppointment    { get; set; } = null!;
}
