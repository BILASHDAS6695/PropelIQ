using System.Text.Json;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Application.Features.SlotSwap;

internal sealed class StaffMediateSwapCommandHandler
    : IRequestHandler<StaffMediateSwapCommand, StaffMediationResultDto>
{
    private readonly IUnitOfWork                             _uow;
    private readonly ICurrentUserService                     _currentUser;
    private readonly IEmailSender                            _email;
    private readonly ILogger<StaffMediateSwapCommandHandler> _logger;

    public StaffMediateSwapCommandHandler(
        IUnitOfWork                              uow,
        ICurrentUserService                      currentUser,
        IEmailSender                             email,
        ILogger<StaffMediateSwapCommandHandler>  logger)
    {
        _uow         = uow;
        _currentUser = currentUser;
        _email       = email;
        _logger      = logger;
    }

    public async Task<StaffMediationResultDto> Handle(
        StaffMediateSwapCommand command,
        CancellationToken       ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException(
                "User must be authenticated to mediate a swap request.");

        var staffUserId = _currentUser.UserId.Value;

        // ── 1. Load swap request with both appointments and requester profile ──
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

        // ── 3. Expiry guard: staff cannot override an expired request ──────
        if (swapRequest.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new ConflictException(
                "Swap request has expired and cannot be overridden. Create a new swap request.");

        // ── 4. Active patient guard (force-approve only) ───────────────────
        if (command.ForceApprove)
        {
            var requesterUser = await _uow.Repository<User>()
                .GetByIdAsync(swapRequest.RequesterPatient.UserId, ct);

            if (requesterUser is not null && !requesterUser.IsActive)
                throw new ConflictException(
                    "Cannot force-approve a swap: the requester patient's account is deactivated.");

            var targetProfiles = await _uow.Repository<PatientProfile>()
                .GetAsync(new PatientProfilesByIdsSpecification([targetAppt.PatientId]), ct);

            if (targetProfiles.Count > 0)
            {
                var targetUser = await _uow.Repository<User>()
                    .GetByIdAsync(targetProfiles[0].UserId, ct);

                if (targetUser is not null && !targetUser.IsActive)
                    throw new ConflictException(
                        "Cannot force-approve a swap: the target patient's account is deactivated.");
            }
        }

        var now = DateTimeOffset.UtcNow;

        // ── 5. Apply the mediation outcome ─────────────────────────────────
        DateTimeOffset? requesterNewSlotTime = null;
        DateTimeOffset? targetNewSlotTime    = null;

        if (command.ForceApprove)
        {
            // Atomically swap slot times and SlotId references
            var originalRequesterSlotId   = requesterAppt.SlotId;
            var originalRequesterSlotTime = requesterAppt.SlotTime;

            requesterAppt.SlotId   = targetAppt.SlotId;
            requesterAppt.SlotTime = targetAppt.SlotTime;

            targetAppt.SlotId   = originalRequesterSlotId;
            targetAppt.SlotTime = originalRequesterSlotTime;

            _uow.Repository<Appointment>().Update(requesterAppt);
            _uow.Repository<Appointment>().Update(targetAppt);

            swapRequest.Status = SlotSwapStatus.StaffApproved;

            requesterNewSlotTime = requesterAppt.SlotTime;
            targetNewSlotTime    = targetAppt.SlotTime;
        }
        else
        {
            swapRequest.Status = SlotSwapStatus.StaffDeclined;
        }

        // ── 6. Stamp mediation metadata ────────────────────────────────────
        swapRequest.OverrideReason   = command.Reason;
        swapRequest.MediatedByUserId = staffUserId;
        swapRequest.OverriddenAt     = now;
        _uow.Repository<SlotSwapRequest>().Update(swapRequest);

        // ── 7. Audit log ───────────────────────────────────────────────────
        var auditDetails = JsonSerializer.Serialize(new
        {
            Action                 = command.ForceApprove ? "StaffForceApprove" : "StaffForceDecline",
            SwapRequestId          = command.SwapRequestId,
            Reason                 = command.Reason,
            RequesterAppointmentId = requesterAppt.Id,
            TargetAppointmentId    = targetAppt.Id,
        });

        await _uow.Repository<AuditLog>().AddAsync(new AuditLog
        {
            Id           = Guid.NewGuid(),
            UserId       = staffUserId,
            Action       = command.ForceApprove ? "StaffForceApproveSwap" : "StaffForceDeclineSwap",
            EntityType   = nameof(SlotSwapRequest),
            EntityId     = swapRequest.Id,
            Timestamp    = now,
            Details      = JsonDocument.Parse(auditDetails),
            PreviousHash = null,
            CurrentHash  = string.Empty,
        }, ct);

        // ── 8. Save — UnitOfWork translates DbUpdateConcurrencyException
        //    (xmin mismatch) into ConflictException automatically. ────────
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Staff user {StaffUserId} {Action} swap request {SwapRequestId}. Reason: {Reason}",
            staffUserId,
            command.ForceApprove ? "force-approved" : "force-declined",
            command.SwapRequestId,
            command.Reason);

        // ── 9. Notify both patients (best-effort; does not re-throw) ───────
        await NotifyBothPatientsAsync(
            command, swapRequest, requesterAppt, targetAppt,
            requesterNewSlotTime, targetNewSlotTime, now, ct);

        return new StaffMediationResultDto(
            SwapRequestId:        swapRequest.Id,
            Status:               swapRequest.Status.ToString(),
            MediatedByUserId:     staffUserId,
            OverriddenAt:         now,
            RequesterNewSlotTime: requesterNewSlotTime,
            TargetNewSlotTime:    targetNewSlotTime);
    }

    private async Task NotifyBothPatientsAsync(
        StaffMediateSwapCommand command,
        SlotSwapRequest         swapRequest,
        Appointment             requesterAppt,
        Appointment             targetAppt,
        DateTimeOffset?         requesterNewSlotTime,
        DateTimeOffset?         targetNewSlotTime,
        DateTimeOffset          now,
        CancellationToken       ct)
    {
        var requesterUser = await _uow.Repository<User>()
            .GetByIdAsync(swapRequest.RequesterPatient.UserId, ct);

        var targetProfiles = await _uow.Repository<PatientProfile>()
            .GetAsync(new PatientProfilesByIdsSpecification([targetAppt.PatientId]), ct);

        User? targetUser = null;
        if (targetProfiles.Count > 0)
            targetUser = await _uow.Repository<User>()
                .GetByIdAsync(targetProfiles[0].UserId, ct);

        if (command.ForceApprove)
        {
            if (requesterUser is not null)
                await _email.SendAsync(
                    requesterUser.Email,
                    "Your slot swap has been approved by staff",
                    $"A staff member has approved your slot swap request. " +
                    $"Your new appointment time is {requesterNewSlotTime:f} UTC.",
                    ct);

            if (targetUser is not null)
                await _email.SendAsync(
                    targetUser.Email,
                    "Your appointment slot has been updated by staff",
                    $"A staff member has reassigned your appointment slot. " +
                    $"Your new appointment time is {targetNewSlotTime:f} UTC.",
                    ct);

            // In-app email notifications
            await _uow.Repository<Notification>().AddAsync(new Notification
            {
                Id             = Guid.NewGuid(),
                PatientId      = requesterAppt.PatientId,
                AppointmentId  = requesterAppt.Id,
                Channel        = NotificationChannel.Email,
                Type           = NotificationType.SlotSwap,
                SentAt         = now,
                DeliveryStatus = DeliveryStatus.Sent,
            }, ct);

            await _uow.Repository<Notification>().AddAsync(new Notification
            {
                Id             = Guid.NewGuid(),
                PatientId      = targetAppt.PatientId,
                AppointmentId  = targetAppt.Id,
                Channel        = NotificationChannel.Email,
                Type           = NotificationType.SlotSwap,
                SentAt         = now,
                DeliveryStatus = DeliveryStatus.Sent,
            }, ct);
        }
        else
        {
            if (requesterUser is not null)
                await _email.SendAsync(
                    requesterUser.Email,
                    "Your slot swap request was declined by staff",
                    $"A staff member has declined your slot swap request. " +
                    $"Reason: {command.Reason}. Your original appointment time is unchanged.",
                    ct);

            if (targetUser is not null)
                await _email.SendAsync(
                    targetUser.Email,
                    "Slot swap request resolved — no change to your appointment",
                    "A staff member has resolved a slot swap request that targeted your appointment. " +
                    "No change was made to your slot.",
                    ct);
        }

        await _uow.SaveChangesAsync(ct);
    }
}
