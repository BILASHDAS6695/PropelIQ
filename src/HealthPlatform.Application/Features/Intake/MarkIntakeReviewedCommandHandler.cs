using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Intake;

/// <summary>
/// Marks an IntakeRecord as ReviewedByProvider.
/// Allowed on Draft records (edge case) — the API layer surfaces a warning header.
/// Sets ReviewedAt and ReviewedByProviderId to the current user's ID.
/// </summary>
internal sealed class MarkIntakeReviewedCommandHandler
    : IRequestHandler<MarkIntakeReviewedCommand>
{
    private readonly IUnitOfWork _uow;

    public MarkIntakeReviewedCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task Handle(MarkIntakeReviewedCommand cmd, CancellationToken ct)
    {
        var records = await _uow.Repository<IntakeRecord>()
            .GetAsync(new IntakeRecordByAppointmentSpecification(cmd.AppointmentId), ct);

        if (records.Count == 0)
            throw new NotFoundException(nameof(IntakeRecord), cmd.AppointmentId);

        var record = records[0];

        record.Status               = IntakeStatus.ReviewedByProvider;
        record.ReviewedAt           = DateTimeOffset.UtcNow;
        record.ReviewedByProviderId = cmd.ReviewerUserId;

        _uow.Repository<IntakeRecord>().Update(record);
        await _uow.SaveChangesAsync(ct);
    }
}
