using System.Text.Json;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Application.Features.SlotSwap;

internal sealed class StaffReassignSlotsCommandHandler
    : IRequestHandler<StaffReassignSlotsCommand, StaffMediationResultDto>
{
    private readonly IUnitOfWork                                 _uow;
    private readonly ICurrentUserService                         _currentUser;
    private readonly IEmailSender                                _email;
    private readonly ILogger<StaffReassignSlotsCommandHandler>   _logger;

    public StaffReassignSlotsCommandHandler(
        IUnitOfWork                                  uow,
        ICurrentUserService                          currentUser,
        IEmailSender                                 email,
        ILogger<StaffReassignSlotsCommandHandler>    logger)
    {
        _uow         = uow;
        _currentUser = currentUser;
        _email       = email;
        _logger      = logger;
    }

    public async Task<StaffMediationResultDto> Handle(
        StaffReassignSlotsCommand command,
        CancellationToken         ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException(
                "User must be authenticated to perform slot reassignment.");

        var staffUserId = _currentUser.UserId.Value;

        // ── 1. Load swap request with both appointments ────────────────────
        var swapRequests = await _uow.Repository<SlotSwapRequest>()
            .GetAsync(new SwapRequestWithAppointmentsSpecification(command.SwapRequestId), ct);

        if (swapRequests.Count == 0)
            throw new NotFoundException(nameof(SlotSwapRequest), command.SwapRequestId);

        var swapRequest   = swapRequests[0];
        var requesterAppt = swapRequest.RequesterAppointment;
        var targetAppt    = swapRequest.TargetAppointment;

        // ── 2. Status guard: must be Pending ──────────────────────────────
        if (swapRequest.Status != SlotSwapStatus.Pending)
            throw new ConflictException(
                $"Swap request is already {swapRequest.Status} and cannot be overridden.");

        // ── 3. Expiry guard ────────────────────────────────────────────────
        if (swapRequest.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new ConflictException(
                "Swap request has expired and cannot be overridden.");

        // ── 4. Load and validate the new target slot ───────────────────────
        var newTargetSlot = await _uow.Repository<AppointmentSlot>()
            .GetByIdAsync(command.NewTargetSlotId, ct);

        if (newTargetSlot is null)
            throw new NotFoundException(nameof(AppointmentSlot), command.NewTargetSlotId);

        if (newTargetSlot.Status != SlotStatus.Available)
            throw new ConflictException(
                "The selected slot for the target patient is not available.");

        // Three-way swap cannot change the provider
        if (newTargetSlot.ProviderId != targetAppt.ProviderId)
            throw new ConflictException(
                "The new target slot must be for the same provider as the original appointment.");

        // ── 5. Active patient guard ────────────────────────────────────────
        var requesterUser = await _uow.Repository<User>()
            .GetByIdAsync(swapRequest.RequesterPatient.UserId, ct);

        var targetProfileList = await _uow.Repository<PatientProfile>()
            .GetAsync(new PatientProfilesByIdsSpecification([targetAppt.PatientId]), ct);

        User? targetUser = null;
        if (targetProfileList.Count > 0)
            targetUser = await _uow.Repository<User>()
                .GetByIdAsync(targetProfileList[0].UserId, ct);

        if (requesterUser is not null && !requesterUser.IsActive)
            throw new ConflictException(
                "Cannot reassign slots: the requester patient's account is deactivated.");

        if (targetUser is not null && !targetUser.IsActive)
            throw new ConflictException(
                "Cannot reassign slots: the target patient's account is deactivated.");

        var now = DateTimeOffset.UtcNow;

        // ── 6. Three-way slot reassignment ─────────────────────────────────
        // Requester takes target's original slot
        var originalRequesterSlotId = requesterAppt.SlotId;

        requesterAppt.SlotId   = targetAppt.SlotId;
        requesterAppt.SlotTime = targetAppt.SlotTime;

        // Target gets the staff-supplied available slot
        targetAppt.SlotId   = newTargetSlot.Id;
        targetAppt.SlotTime = newTargetSlot.StartTime;

        // Mark new slot as Booked
        newTargetSlot.Status = SlotStatus.Booked;
        _uow.Repository<AppointmentSlot>().Update(newTargetSlot);

        // Release requester's original slot back to Available (if it was a regular slot)
        if (originalRequesterSlotId.HasValue)
        {
            var releasedSlot = await _uow.Repository<AppointmentSlot>()
                .GetByIdAsync(originalRequesterSlotId.Value, ct);
            if (releasedSlot is not null)
            {
                releasedSlot.Status = SlotStatus.Available;
                _uow.Repository<AppointmentSlot>().Update(releasedSlot);
            }
        }

        _uow.Repository<Appointment>().Update(requesterAppt);
        _uow.Repository<Appointment>().Update(targetAppt);

        // ── 7. Stamp mediation metadata ────────────────────────────────────
        swapRequest.Status                  = SlotSwapStatus.StaffReassigned;
        swapRequest.OverrideReason          = command.Reason;
        swapRequest.MediatedByUserId        = staffUserId;
        swapRequest.OverriddenAt            = now;
        swapRequest.ThreeWayNewTargetSlotId = command.NewTargetSlotId;
        _uow.Repository<SlotSwapRequest>().Update(swapRequest);

        // ── 8. Audit log ───────────────────────────────────────────────────
        var auditDetails = JsonSerializer.Serialize(new
        {
            Action                 = "StaffThreeWayReassign",
            SwapRequestId          = command.SwapRequestId,
            Reason                 = command.Reason,
            RequesterAppointmentId = requesterAppt.Id,
            TargetAppointmentId    = targetAppt.Id,
            NewTargetSlotId        = command.NewTargetSlotId,
        });

        await _uow.Repository<AuditLog>().AddAsync(new AuditLog
        {
            Id           = Guid.NewGuid(),
            UserId       = staffUserId,
            Action       = "StaffThreeWaySlotReassign",
            EntityType   = nameof(SlotSwapRequest),
            EntityId     = swapRequest.Id,
            Timestamp    = now,
            Details      = JsonDocument.Parse(auditDetails),
            PreviousHash = null,
            CurrentHash  = string.Empty,
        }, ct);

        // ── 9. Save — UnitOfWork translates DbUpdateConcurrencyException
        //    (xmin mismatch) into ConflictException automatically. ────────
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Staff user {StaffUserId} performed three-way reassignment on swap {SwapRequestId}. " +
            "NewTargetSlotId: {NewTargetSlotId}. Reason: {Reason}",
            staffUserId, command.SwapRequestId, command.NewTargetSlotId, command.Reason);

        // ── 10. Notify both patients ───────────────────────────────────────
        if (requesterUser is not null)
            await _email.SendAsync(
                requesterUser.Email,
                "Your appointment slot has been updated by staff",
                $"A staff member has reassigned your appointment as part of a scheduling adjustment. " +
                $"Your new appointment time is {requesterAppt.SlotTime:f} UTC.",
                ct);

        if (targetUser is not null)
            await _email.SendAsync(
                targetUser.Email,
                "Your appointment slot has been updated by staff",
                $"A staff member has reassigned your appointment as part of a scheduling adjustment. " +
                $"Your new appointment time is {targetAppt.SlotTime:f} UTC.",
                ct);

        await _uow.SaveChangesAsync(ct);

        return new StaffMediationResultDto(
            SwapRequestId:        swapRequest.Id,
            Status:               swapRequest.Status.ToString(),
            MediatedByUserId:     staffUserId,
            OverriddenAt:         now,
            RequesterNewSlotTime: requesterAppt.SlotTime,
            TargetNewSlotTime:    targetAppt.SlotTime);
    }
}
