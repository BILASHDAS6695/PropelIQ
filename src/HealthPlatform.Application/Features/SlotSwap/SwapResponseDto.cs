namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Result returned after a target patient accepts or declines a swap request.
/// </summary>
public sealed record SwapResponseDto(
    Guid            SwapRequestId,
    string          Status,
    DateTimeOffset? RequesterNewSlotTime,
    DateTimeOffset? TargetNewSlotTime);
