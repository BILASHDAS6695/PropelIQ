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

    // ── Cancellation ──────────────────────────────────────────────────────
    /// <summary>Populated when Status is Cancelled or when rescheduled.</summary>
    public CancellationReason? CancellationReason { get; set; }
    /// <summary>Optional free-text note; required by the UI when Reason = Other.</summary>
    public string? CancellationNote { get; set; }

    // ── Conflict override ─────────────────────────────────────────────────
    /// <summary>True when a staff member force-booked despite a hard conflict.</summary>
    public bool    IsConflictOverride     { get; set; }
    /// <summary>Mandatory justification supplied by staff when IsConflictOverride is true.</summary>
    public string? ConflictOverrideReason { get; set; }

    public PatientProfile   Patient  { get; set; } = null!;
    public Provider         Provider { get; set; } = null!;
    public AppointmentSlot? Slot     { get; set; }     // null for walk-ins
    public IntakeRecord? IntakeRecord { get; set; }
    public PreferredSlotPreference? PreferredSlotPreference { get; set; }
    public ICollection<Notification>    Notifications          { get; set; } = [];

    // Swap requests initiated BY this appointment (requester side)
    public ICollection<SlotSwapRequest> InitiatedSwapRequests { get; set; } = [];
    // Swap requests targeting THIS appointment (target side)
    public ICollection<SlotSwapRequest> ReceivedSwapRequests  { get; set; } = [];
}
