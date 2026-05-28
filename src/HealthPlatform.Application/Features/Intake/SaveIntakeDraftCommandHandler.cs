using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Intake;

/// <summary>
/// Upserts an IntakeRecord in Draft status.
/// Only one active draft per appointment is kept — existing Draft is overwritten.
/// Completed intakes cannot be overwritten.
/// </summary>
internal sealed class SaveIntakeDraftCommandHandler
    : IRequestHandler<SaveIntakeDraftCommand, Guid>
{
    private readonly IUnitOfWork _uow;

    public SaveIntakeDraftCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Guid> Handle(SaveIntakeDraftCommand cmd, CancellationToken ct)
    {
        var patientProfiles = await _uow.Repository<PatientProfile>()
            .GetAsync(new PatientProfileByUserIdSpecification(cmd.PatientUserId), ct);

        if (patientProfiles.Count == 0)
            throw new NotFoundException(nameof(PatientProfile), cmd.PatientUserId);

        var patientId = patientProfiles[0].Id;

        var existing = await _uow.Repository<IntakeRecord>()
            .GetAsync(new IntakeRecordByAppointmentSpecification(cmd.AppointmentId), ct);

        if (existing.Count > 0 && existing[0].Status == IntakeStatus.Completed)
            throw new ConflictException("Intake has already been submitted and cannot be edited.");

        IntakeRecord record;
        if (existing.Count > 0)
        {
            record      = existing[0];
            record.Data = cmd.Data;
            record.Mode = cmd.Mode;
            _uow.Repository<IntakeRecord>().Update(record);
        }
        else
        {
            record = new IntakeRecord
            {
                PatientId     = patientId,
                AppointmentId = cmd.AppointmentId,
                Mode          = cmd.Mode,
                Status        = IntakeStatus.Draft,
                Data          = cmd.Data,
            };
            await _uow.Repository<IntakeRecord>().AddAsync(record, ct);
        }

        await _uow.SaveChangesAsync(ct);
        return record.Id;
    }
}
