namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Anonymized view of a booked appointment slot available for swap.
/// Patient identity is intentionally omitted for privacy.
/// </summary>
public sealed record SwappableSlotDto(
    Guid           AppointmentId,
    DateTimeOffset SlotTime);
