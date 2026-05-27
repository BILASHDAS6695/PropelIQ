using MediatR;

namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Staff command to perform a three-way slot reassignment:
/// the requester takes the target's current slot and the target is moved
/// to a staff-supplied available slot.
/// </summary>
/// <param name="SwapRequestId">Pending swap request to resolve via reassignment.</param>
/// <param name="NewTargetSlotId">
///   ID of an available <see cref="HealthPlatform.Domain.Entities.AppointmentSlot"/>
///   to assign to the target patient's appointment.
/// </param>
/// <param name="Reason">
///   Mandatory justification text. Stored on the swap request and in the audit log.
/// </param>
public sealed record StaffReassignSlotsCommand(
    Guid   SwapRequestId,
    Guid   NewTargetSlotId,
    string Reason) : IRequest<StaffMediationResultDto>;
