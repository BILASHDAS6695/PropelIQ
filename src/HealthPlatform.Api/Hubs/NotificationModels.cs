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
