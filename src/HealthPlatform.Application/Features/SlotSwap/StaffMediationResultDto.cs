namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Returned by all staff mediation commands (force-approve, force-decline,
/// three-way reassignment) to confirm the outcome.
/// </summary>
public sealed record StaffMediationResultDto(
    Guid            SwapRequestId,
    string          Status,
    Guid            MediatedByUserId,
    DateTimeOffset  OverriddenAt,
    DateTimeOffset? RequesterNewSlotTime,
    DateTimeOffset? TargetNewSlotTime);
