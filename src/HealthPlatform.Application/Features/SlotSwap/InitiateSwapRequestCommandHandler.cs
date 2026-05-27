using System.Text.Json;
using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Application.Features.SlotSwap;

internal sealed class InitiateSwapRequestCommandHandler
    : IRequestHandler<InitiateSwapRequestCommand, SwapRequestDto>
{
    private static readonly TimeSpan SwapTtl = TimeSpan.FromHours(24);

    private readonly IUnitOfWork                                _uow;
    private readonly ICurrentUserService                        _currentUser;
    private readonly ILogger<InitiateSwapRequestCommandHandler> _logger;

    public InitiateSwapRequestCommandHandler(
        IUnitOfWork                                 uow,
        ICurrentUserService                         currentUser,
        ILogger<InitiateSwapRequestCommandHandler>  logger)
    {
        _uow         = uow;
        _currentUser = currentUser;
        _logger      = logger;
    }

    public async Task<SwapRequestDto> Handle(
        InitiateSwapRequestCommand command,
        CancellationToken          ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("User must be authenticated to initiate a swap request.");

        // ── 0. Resolve caller's patient profile ───────────────────────────
        var patientProfiles = await _uow.Repository<PatientProfile>()
            .GetAsync(new PatientProfileByUserIdSpecification(_currentUser.UserId.Value), ct);

        if (patientProfiles.Count == 0)
            throw new NotFoundException(nameof(PatientProfile), _currentUser.UserId.Value);

        var patient = patientProfiles[0];

        var apptRepo = _uow.Repository<Appointment>();
        var swapRepo = _uow.Repository<SlotSwapRequest>();

        // ── 1. Load and validate requester's appointment ──────────────────
        var requesterAppt = await apptRepo.GetByIdAsync(command.RequesterAppointmentId, ct)
            ?? throw new NotFoundException(nameof(Appointment), command.RequesterAppointmentId);

        if (requesterAppt.PatientId != patient.Id)
            throw new ForbiddenAccessException("Appointment does not belong to the requesting patient.");

        if (requesterAppt.Status != AppointmentStatus.Scheduled || requesterAppt.IsWalkIn)
            throw new ConflictException("Only active scheduled appointments can initiate a swap.");

        // ── 2. Enforce one-active-swap-per-appointment ────────────────────
        var existingSpec = new ActiveSwapRequestByAppointmentSpecification(
            command.RequesterAppointmentId);
        var existing = await swapRepo.GetAsync(existingSpec, ct);

        if (existing.Count > 0)
            throw new ConflictException(
                "This appointment already has a pending swap request. " +
                "Cancel the existing request before creating a new one.");

        // ── 3. Load and validate target appointment ───────────────────────
        var targetAppt = await apptRepo.GetByIdAsync(command.TargetAppointmentId, ct)
            ?? throw new NotFoundException(nameof(Appointment), command.TargetAppointmentId);

        if (targetAppt.ProviderId != requesterAppt.ProviderId)
            throw new ConflictException("Swap target must be with the same provider.");

        if (targetAppt.Status != AppointmentStatus.Scheduled || targetAppt.IsWalkIn)
            throw new ConflictException("Target appointment is not eligible for swap.");

        // ── 4. Create the swap request ────────────────────────────────────
        var now     = DateTimeOffset.UtcNow;
        var request = new SlotSwapRequest
        {
            Id                     = Guid.NewGuid(),
            RequesterPatientId     = patient.Id,
            RequesterAppointmentId = command.RequesterAppointmentId,
            TargetAppointmentId    = command.TargetAppointmentId,
            Status                 = SlotSwapStatus.Pending,
            ExpiresAt              = now.Add(SwapTtl),
        };

        await swapRepo.AddAsync(request, ct);

        // ── 5. Audit log ──────────────────────────────────────────────────
        var auditDetails = JsonSerializer.Serialize(new
        {
            RequesterAppointmentId = command.RequesterAppointmentId,
            TargetAppointmentId    = command.TargetAppointmentId,
            ExpiresAt              = request.ExpiresAt,
        });

        await _uow.Repository<AuditLog>().AddAsync(new AuditLog
        {
            UserId      = patient.Id,
            Action      = "SlotSwapRequested",
            EntityType  = nameof(SlotSwapRequest),
            EntityId    = request.Id,
            Timestamp   = now,
            Details     = JsonDocument.Parse(auditDetails),
            CurrentHash = string.Empty,
        }, ct);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Swap request {SwapId} created by patient {PatientId} targeting appointment {TargetId}",
            request.Id, patient.Id, command.TargetAppointmentId);

        return new SwapRequestDto(
            request.Id,
            requesterAppt.SlotTime,
            targetAppt.SlotTime,
            request.Status.ToString(),
            request.ExpiresAt);
    }
}
