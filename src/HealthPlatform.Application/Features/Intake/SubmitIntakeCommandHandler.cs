using System.Text.Json;
using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Intake;

/// <summary>
/// Finalises an intake submission:
/// 1. Validates patient exists and intake is not already completed.
/// 2. Upserts IntakeRecord with Status = Completed, stamps CompletedAt.
/// 3. Writes AuditLog entry (Action = "IntakeCompleted").
/// </summary>
internal sealed class SubmitIntakeCommandHandler
    : IRequestHandler<SubmitIntakeCommand, Guid>
{
    private readonly IUnitOfWork _uow;

    public SubmitIntakeCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Guid> Handle(SubmitIntakeCommand cmd, CancellationToken ct)
    {
        var patientProfiles = await _uow.Repository<PatientProfile>()
            .GetAsync(new PatientProfileByUserIdSpecification(cmd.PatientUserId), ct);

        if (patientProfiles.Count == 0)
            throw new NotFoundException(nameof(PatientProfile), cmd.PatientUserId);

        var patientId = patientProfiles[0].Id;

        var existing = await _uow.Repository<IntakeRecord>()
            .GetAsync(new IntakeRecordByAppointmentSpecification(cmd.AppointmentId), ct);

        if (existing.Count > 0 && existing[0].Status == IntakeStatus.Completed)
            throw new ConflictException("Intake has already been submitted.");

        var now = DateTimeOffset.UtcNow;
        IntakeRecord record;

        if (existing.Count > 0)
        {
            record             = existing[0];
            record.Data        = cmd.Data;
            record.Mode        = cmd.Mode;
            record.Status      = IntakeStatus.Completed;
            record.CompletedAt = now;
            _uow.Repository<IntakeRecord>().Update(record);
        }
        else
        {
            record = new IntakeRecord
            {
                PatientId     = patientId,
                AppointmentId = cmd.AppointmentId,
                Mode          = cmd.Mode,
                Status        = IntakeStatus.Completed,
                Data          = cmd.Data,
                CompletedAt   = now,
            };
            await _uow.Repository<IntakeRecord>().AddAsync(record, ct);
        }

        // Audit log
        var details = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            appointmentId = cmd.AppointmentId,
            mode          = cmd.Mode.ToString(),
            completedAt   = now,
        }));

        await _uow.Repository<AuditLog>().AddAsync(new AuditLog
        {
            UserId      = cmd.PatientUserId,
            Action      = "IntakeCompleted",
            EntityType  = nameof(IntakeRecord),
            EntityId    = record.Id,
            Timestamp   = now,
            Details     = details,
            CurrentHash = string.Empty,
        }, ct);

        await _uow.SaveChangesAsync(ct);
        return record.Id;
    }
}
