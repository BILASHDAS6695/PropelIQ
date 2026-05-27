using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Application.Features.SlotSwap;

internal sealed class CancelSwapRequestCommandHandler
    : IRequestHandler<CancelSwapRequestCommand>
{
    private readonly IUnitOfWork                               _uow;
    private readonly ICurrentUserService                       _currentUser;
    private readonly ILogger<CancelSwapRequestCommandHandler>  _logger;

    public CancelSwapRequestCommandHandler(
        IUnitOfWork                                uow,
        ICurrentUserService                        currentUser,
        ILogger<CancelSwapRequestCommandHandler>  logger)
    {
        _uow         = uow;
        _currentUser = currentUser;
        _logger      = logger;
    }

    public async Task Handle(CancelSwapRequestCommand command, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("User must be authenticated to cancel a swap request.");

        // ── 0. Resolve caller's patient profile ───────────────────────────
        var patientProfiles = await _uow.Repository<PatientProfile>()
            .GetAsync(new PatientProfileByUserIdSpecification(_currentUser.UserId.Value), ct);

        if (patientProfiles.Count == 0)
            throw new NotFoundException(nameof(PatientProfile), _currentUser.UserId.Value);

        var patient = patientProfiles[0];

        var swapRepo = _uow.Repository<SlotSwapRequest>();

        var request = await swapRepo.GetByIdAsync(command.SwapRequestId, ct)
            ?? throw new NotFoundException(nameof(SlotSwapRequest), command.SwapRequestId);

        // ── Ownership check ───────────────────────────────────────────────
        if (request.RequesterPatientId != patient.Id)
            throw new ForbiddenAccessException("Cannot cancel a swap request you did not initiate.");

        // ── Status guard ──────────────────────────────────────────────────
        if (request.Status != SlotSwapStatus.Pending)
            throw new ConflictException(
                $"Swap request is already {request.Status} and cannot be cancelled.");

        request.Status             = SlotSwapStatus.Cancelled;
        request.CancellationReason = command.Reason;

        swapRepo.Update(request);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Swap request {SwapId} cancelled by patient {PatientId}",
            command.SwapRequestId, patient.Id);
    }
}
