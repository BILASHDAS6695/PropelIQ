namespace HealthPlatform.Application.Features.SlotSwap;

public sealed record SwapRequestDto(
    Guid           SwapRequestId,
    DateTimeOffset RequesterSlotTime,
    DateTimeOffset TargetSlotTime,
    string         Status,
    DateTimeOffset ExpiresAt);
