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
