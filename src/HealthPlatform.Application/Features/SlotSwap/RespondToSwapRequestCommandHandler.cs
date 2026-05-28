using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Application.Features.SlotSwap;

internal sealed class RespondToSwapRequestCommandHandler
    : IRequestHandler<RespondToSwapRequestCommand, SwapResponseDto>
{
    private readonly IUnitOfWork                                  _uow;
    private readonly ICurrentUserService                          _currentUser;
    private readonly IEmailSender                                 _email;
    private readonly IInAppNotifier                               _inAppNotifier;
    private readonly INotificationPreferenceChecker               _prefChecker;
    private readonly ILogger<RespondToSwapRequestCommandHandler>  _logger;

    public RespondToSwapRequestCommandHandler(
        IUnitOfWork                                   uow,
        ICurrentUserService                           currentUser,
        IEmailSender                                  email,
        IInAppNotifier                                inAppNotifier,
        INotificationPreferenceChecker                prefChecker,
        ILogger<RespondToSwapRequestCommandHandler>   logger)
    {
        _uow           = uow;
        _currentUser   = currentUser;
        _email         = email;
        _inAppNotifier = inAppNotifier;
        _prefChecker   = prefChecker;
        _logger        = logger;
    }

    public async Task<SwapResponseDto> Handle(
        RespondToSwapRequestCommand command,
        CancellationToken           ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException(
                "User must be authenticated to respond to a swap request.");

        // ── 0. Resolve caller's patient profile ───────────────────────────
        var profiles = await _uow.Repository<PatientProfile>()
            .GetAsync(new PatientProfileByUserIdSpecification(_currentUser.UserId.Value), ct);

        if (profiles.Count == 0)
            throw new NotFoundException(nameof(PatientProfile), _currentUser.UserId.Value);

        var callerPatient = profiles[0];

        // ── 1. Load swap request with both appointments ───────────────────
        var swapRequests = await _uow.Repository<SlotSwapRequest>()
            .GetAsync(new SwapRequestWithAppointmentsSpecification(command.SwapRequestId), ct);

        if (swapRequests.Count == 0)
            throw new NotFoundException(nameof(SlotSwapRequest), command.SwapRequestId);

        var swapRequest   = swapRequests[0];
        var requesterAppt = swapRequest.RequesterAppointment;
        var targetAppt    = swapRequest.TargetAppointment;

        // ── 2. Ownership check: caller must own the TARGET appointment ─────
        if (targetAppt.PatientId != callerPatient.Id)
            throw new ForbiddenAccessException(
                "Only the target patient can respond to this swap request.");

        // ── 3. Status + expiry guards ─────────────────────────────────────
        if (swapRequest.Status != SlotSwapStatus.Pending)
            throw new ConflictException(
                $"Swap request is already {swapRequest.Status} and cannot be responded to.");

        if (swapRequest.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new ConflictException("Swap request has expired.");

        // ── 4. Load User entities for email addresses ─────────────────────
        var requesterUser = await _uow.Repository<User>()
            .GetByIdAsync(swapRequest.RequesterPatient.UserId, ct);
        var targetUser = await _uow.Repository<User>()
            .GetByIdAsync(callerPatient.UserId, ct);

        var apptRepo = _uow.Repository<Appointment>();
        var now      = DateTimeOffset.UtcNow;

        // ── 5. Branch: Accept vs Decline ──────────────────────────────────
        if (command.Accept)
        {
            // Re-check both appointments are still eligible (concurrency guard)
            if (requesterAppt.Status != AppointmentStatus.Scheduled || requesterAppt.IsWalkIn)
                throw new ConflictException(
                    "Requester's appointment is no longer eligible for swap.");

            if (targetAppt.Status != AppointmentStatus.Scheduled || targetAppt.IsWalkIn)
                throw new ConflictException(
                    "Target appointment is no longer eligible for swap.");

            // Atomic slot swap — single SaveChangesAsync below commits both
            (requesterAppt.SlotId,   targetAppt.SlotId)  = (targetAppt.SlotId,   requesterAppt.SlotId);
            (requesterAppt.SlotTime, targetAppt.SlotTime) = (targetAppt.SlotTime, requesterAppt.SlotTime);

            apptRepo.Update(requesterAppt);
            apptRepo.Update(targetAppt);

            swapRequest.Status = SlotSwapStatus.Accepted;

            var notifRepo = _uow.Repository<Notification>();

            await notifRepo.AddAsync(new Notification
            {
                Id             = Guid.NewGuid(),
                UserId         = swapRequest.RequesterPatient.UserId,
                PatientId      = swapRequest.RequesterPatientId,
                AppointmentId  = requesterAppt.Id,
                Channel        = NotificationChannel.Email,
                Type           = NotificationType.SlotSwap,
                Title          = "Slot swap accepted",
                Message        = $"Your slot swap request was accepted. New time: {requesterAppt.SlotTime:f} UTC.",
                SentAt         = now,
                DeliveryStatus = DeliveryStatus.Sent,
                ExpiresAt      = now.AddDays(90),
            }, ct);

            await notifRepo.AddAsync(new Notification
            {
                Id             = Guid.NewGuid(),
                UserId         = callerPatient.UserId,
                PatientId      = callerPatient.Id,
                AppointmentId  = targetAppt.Id,
                Channel        = NotificationChannel.Email,
                Type           = NotificationType.SlotSwap,
                Title          = "Slot swap confirmed",
                Message        = $"You accepted a slot swap. New time: {targetAppt.SlotTime:f} UTC.",
                SentAt         = now,
                DeliveryStatus = DeliveryStatus.Sent,
                ExpiresAt      = now.AddDays(90),
            }, ct);

            if (requesterUser is not null)
                if (await _prefChecker.IsAllowedAsync(
                        swapRequest.RequesterPatient.UserId, NotificationChannel.Email, NotificationType.SwapResult, ct))
                    await _email.SendAsync(
                        requesterUser.Email,
                        "Slot swap accepted \u2014 your appointment time has changed",
                        $"Your slot swap request was accepted. " +
                        $"Your new appointment time is {requesterAppt.SlotTime:f} UTC.",
                        ct);

            if (targetUser is not null)
                if (await _prefChecker.IsAllowedAsync(
                        callerPatient.UserId, NotificationChannel.Email, NotificationType.SwapResult, ct))
                    await _email.SendAsync(
                        targetUser.Email,
                        "Slot swap confirmed \u2014 your appointment time has changed",
                        $"You accepted a slot swap. " +
                        $"Your new appointment time is {targetAppt.SlotTime:f} UTC.",
                        ct);

            _logger.LogInformation(
                "Swap request {SwapId} accepted. Requester appt {ReqId} ↔ Target appt {TgtId}",
                command.SwapRequestId, requesterAppt.Id, targetAppt.Id);
        }
        else
        {
            swapRequest.Status             = SlotSwapStatus.Declined;
            swapRequest.CancellationReason = command.Reason;

            // Notify requester only
            await _uow.Repository<Notification>().AddAsync(new Notification
            {
                Id             = Guid.NewGuid(),
                UserId         = swapRequest.RequesterPatient.UserId,
                PatientId      = swapRequest.RequesterPatientId,
                AppointmentId  = requesterAppt.Id,
                Channel        = NotificationChannel.Email,
                Type           = NotificationType.SlotSwap,
                Title          = "Slot swap declined",
                Message        = "Your slot swap request was declined by the other patient.",
                SentAt         = now,
                DeliveryStatus = DeliveryStatus.Sent,
                ExpiresAt      = now.AddDays(90),
            }, ct);

            if (requesterUser is not null)
                if (await _prefChecker.IsAllowedAsync(
                        swapRequest.RequesterPatient.UserId, NotificationChannel.Email, NotificationType.SwapResult, ct))
                    await _email.SendAsync(
                        requesterUser.Email,
                        "Slot swap declined",
                        "Your slot swap request was declined by the other patient.",
                    ct);

            _logger.LogInformation(
                "Swap request {SwapId} declined by target patient {PatientId}",
                command.SwapRequestId, callerPatient.Id);
        }

        _uow.Repository<SlotSwapRequest>().Update(swapRequest);
        await _uow.SaveChangesAsync(ct);

        // ── 6. In-app notifications (after commit) ────────────────────────
        if (command.Accept)
        {
            await _inAppNotifier.NotifyAsync(
                swapRequest.RequesterPatient.UserId,
                swapRequest.RequesterPatientId,
                requesterAppt.Id,
                NotificationType.SwapResult,
                "Slot swap accepted",
                $"Your slot swap request was accepted. New time: {requesterAppt.SlotTime:f} UTC.",
                $"/appointments/{requesterAppt.Id}",
                ct);

            await _inAppNotifier.NotifyAsync(
                callerPatient.UserId,
                callerPatient.Id,
                targetAppt.Id,
                NotificationType.SwapResult,
                "Slot swap confirmed",
                $"You accepted a slot swap. New time: {targetAppt.SlotTime:f} UTC.",
                $"/appointments/{targetAppt.Id}",
                ct);
        }
        else
        {
            await _inAppNotifier.NotifyAsync(
                swapRequest.RequesterPatient.UserId,
                swapRequest.RequesterPatientId,
                requesterAppt.Id,
                NotificationType.SwapResult,
                "Slot swap declined",
                "Your slot swap request was declined by the other patient.",
                $"/appointments/{requesterAppt.Id}",
                ct);
        }

        return new SwapResponseDto(
            swapRequest.Id,
            swapRequest.Status.ToString(),
            command.Accept ? requesterAppt.SlotTime : null,
            command.Accept ? targetAppt.SlotTime    : null);
    }
}
