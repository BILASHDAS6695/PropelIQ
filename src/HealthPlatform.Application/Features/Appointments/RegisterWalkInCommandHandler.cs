using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Registers a walk-in appointment for an existing patient with a given provider.
///
/// Flow:
/// 1. Validate provider exists.
/// 2. Validate patient profile exists.
/// 3. Compute next queue position: MAX(queue_position) + 1 for provider today.
/// 4. Create Appointment: Status = WalkIn, IsWalkIn = true, SlotId = null,
///    ArrivalTime = SlotTime = UtcNow, QueuePosition = computed.
/// 5. Persist. Audit log entry created automatically by AuditSaveChangesInterceptor.
/// </summary>
internal sealed class RegisterWalkInCommandHandler
    : IRequestHandler<RegisterWalkInCommand, WalkInConfirmationDto>
{
    private readonly IUnitOfWork _uow;

    public RegisterWalkInCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<WalkInConfirmationDto> Handle(
        RegisterWalkInCommand command,
        CancellationToken     ct)
    {
        // ── 1. Validate provider ───────────────────────────────────────────
        var provider = await _uow.Repository<Provider>()
            .GetByIdAsync(command.ProviderId, ct)
            ?? throw new NotFoundException(nameof(Provider), command.ProviderId);

        // ── 2. Validate patient profile ────────────────────────────────────
        var patientProfiles = await _uow.Repository<PatientProfile>()
            .GetAsync(new PatientProfileByPatientIdSpecification(command.PatientId), ct);

        if (patientProfiles.Count == 0)
            throw new NotFoundException(nameof(PatientProfile), command.PatientId);

        var patient = patientProfiles[0];

        // ── 3. Compute queue position ─────────────────────────────────────
        var today      = DateOnly.FromDateTime(DateTime.UtcNow);
        var queueItems = await _uow.Repository<Appointment>()
            .GetAsync(new WalkInQueuePositionSpecification(command.ProviderId, today), ct);

        int nextPosition = queueItems.Count == 0
            ? 1
            : queueItems.Max(a => a.QueuePosition ?? 0) + 1;

        // ── 4. Create walk-in appointment ─────────────────────────────────
        var now = DateTimeOffset.UtcNow;
        var appointment = new Appointment
        {
            Id            = Guid.NewGuid(),
            PatientId     = patient.Id,
            ProviderId    = command.ProviderId,
            SlotId        = null,              // walk-ins have no pre-booked slot
            SlotTime      = now,
            ArrivalTime   = now,
            Status        = AppointmentStatus.WalkIn,
            IsWalkIn      = true,
            QueuePosition = nextPosition,
            VisitReason   = command.VisitReason
        };

        await _uow.Repository<Appointment>().AddAsync(appointment, ct);
        await _uow.SaveChangesAsync(ct);

        return new WalkInConfirmationDto(
            appointment.Id,
            patient.Id,
            provider.Id,
            provider.Name,
            nextPosition,
            now,
            appointment.Status.ToString());
    }
}
