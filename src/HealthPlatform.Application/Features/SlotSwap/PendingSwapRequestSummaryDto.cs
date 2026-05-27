namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Staff-only view of a pending slot swap request.
/// Both patient names are exposed because staff are authorized to see them.
/// </summary>
public sealed record PendingSwapRequestSummaryDto(
    Guid           SwapRequestId,
    Guid           RequesterPatientId,
    string         RequesterFullName,
    DateTimeOffset RequesterSlotTime,
    Guid           TargetPatientId,
    string         TargetFullName,
    DateTimeOffset TargetSlotTime,
    DateTimeOffset ExpiresAt);
