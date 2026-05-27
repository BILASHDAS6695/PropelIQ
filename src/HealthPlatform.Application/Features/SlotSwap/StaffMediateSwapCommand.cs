using MediatR;

namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Staff command to force-approve or force-decline a pending slot swap request,
/// bypassing the target patient's consent.
/// </summary>
/// <param name="SwapRequestId">ID of the pending swap request to mediate.</param>
/// <param name="ForceApprove">
///   <c>true</c> to force-approve (slots are swapped immediately);
///   <c>false</c> to force-decline (swap is rejected on behalf of the target patient).
/// </param>
/// <param name="Reason">
///   Mandatory justification text. Stored on the swap request and in the audit log.
/// </param>
public sealed record StaffMediateSwapCommand(
    Guid   SwapRequestId,
    bool   ForceApprove,
    string Reason) : IRequest<StaffMediationResultDto>;
