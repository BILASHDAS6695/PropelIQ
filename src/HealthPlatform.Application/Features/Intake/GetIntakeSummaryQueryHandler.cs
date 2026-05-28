using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.Intake;

internal sealed class GetIntakeSummaryQueryHandler
    : IRequestHandler<GetIntakeSummaryQuery, IntakeSummaryDto?>
{
    private readonly IUnitOfWork _uow;

    public GetIntakeSummaryQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IntakeSummaryDto?> Handle(GetIntakeSummaryQuery query, CancellationToken ct)
    {
        var records = await _uow.Repository<IntakeRecord>()
            .GetAsync(new IntakeRecordByAppointmentSpecification(query.AppointmentId), ct);

        if (records.Count == 0) return null;

        var r = records[0];
        return new IntakeSummaryDto(
            r.Id,
            r.AppointmentId,
            r.PatientId,
            r.Mode,
            r.Status,
            r.Data,
            r.CompletedAt,
            r.ReviewedAt,
            r.ReviewedByProviderId);
    }
}
