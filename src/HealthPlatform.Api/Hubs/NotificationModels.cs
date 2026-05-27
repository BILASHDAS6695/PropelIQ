namespace HealthPlatform.Api.Hubs;

/// <summary>Payload broadcast when a provider's slot availability changes.</summary>
public sealed record SlotAvailabilityChangedPayload(
    Guid ProviderId,
    DateOnly Date,
    int AvailableSlots);

/// <summary>Payload broadcast when a provider's queue status is updated.</summary>
public sealed record QueueStatusUpdatedPayload(
    Guid ProviderId,
    int QueueLength,
    int EstimatedWaitMinutes);

/// <summary>
/// Broadcast to a provider's SignalR group when a patient checks in at
/// the front desk.  The <see cref="IsLateArrival"/> flag drives the
/// "Late Arrival" visual indicator on the provider's dashboard.
/// </summary>
public sealed record PatientArrivedPayload(
    Guid           AppointmentId,
    Guid           ProviderId,
    Guid           PatientId,
    string         PatientFullName,
    DateTimeOffset ArrivalTime,
    bool           IsLateArrival);

/// <summary>
/// Broadcast to a provider's SignalR group when a provider changes an
/// appointment status (e.g. Arrived → InProgress → Completed).
/// </summary>
public sealed record AppointmentStatusChangedPayload(
    Guid   AppointmentId,
    Guid   ProviderId,
    string OldStatus,
    string NewStatus);

/// <summary>
/// Broadcast to the staff-notifications SignalR group when a staff member
/// force-books an appointment despite a hard scheduling conflict.
/// </summary>
public sealed record ConflictOverrideUsedPayload(
    Guid   AppointmentId,
    Guid   PatientId,
    Guid   ProviderId,
    string OverrideReason,
    string ConflictSummary);

/// <summary>
/// Broadcast to a provider's SignalR group when a patient is marked as
/// NoShow (manually by staff or automatically by the Hangfire job).
/// The <see cref="IsAutomatic"/> flag lets the UI differentiate the source.
/// The associated slot has already been freed at this point.
/// </summary>
public sealed record AppointmentNoShowPayload(
    Guid           AppointmentId,
    Guid           ProviderId,
    Guid           PatientId,
    DateTimeOffset SlotTime,
    bool           IsAutomatic,
    int            PatientTotalNoShowCount);
