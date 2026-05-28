using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Intake;

internal sealed class TriggerWalkInIntakeCommandHandler
    : IRequestHandler<TriggerWalkInIntakeCommand, Guid>
{
    private readonly IUnitOfWork _uow;

    public TriggerWalkInIntakeCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Guid> Handle(TriggerWalkInIntakeCommand cmd, CancellationToken ct)
    {
        // Idempotency: return existing Draft if present
        var existing = await _uow.Repository<IntakeRecord>()
            .GetAsync(new IntakeRecordByAppointmentSpecification(cmd.AppointmentId), ct);

        if (existing.Count > 0 && existing[0].Status == IntakeStatus.Draft)
            return existing[0].Id;

        // Load the appointment to get PatientId
        var appointments = await _uow.Repository<Appointment>()
            .GetAsync(new AppointmentWithIntakeSpecification(cmd.AppointmentId), ct);

        if (appointments.Count == 0)
            throw new InvalidOperationException(
                $"Appointment {cmd.AppointmentId} not found.");

        var appt = appointments[0];

        var record = new IntakeRecord
        {
            PatientId     = appt.PatientId,
            AppointmentId = cmd.AppointmentId,
            Mode          = IntakeMode.ManualForm,
            Status        = IntakeStatus.Draft,
        };

        await _uow.Repository<IntakeRecord>().AddAsync(record, ct);
        await _uow.SaveChangesAsync(ct);

        return record.Id;
    }
}
