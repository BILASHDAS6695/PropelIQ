using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.Intake;

internal sealed class GetIntakeWindowQueryHandler
    : IRequestHandler<GetIntakeWindowQuery, IntakeWindowResult?>
{
    private readonly IUnitOfWork _uow;

    public GetIntakeWindowQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IntakeWindowResult?> Handle(
        GetIntakeWindowQuery query,
        CancellationToken    ct)
    {
        var results = await _uow.Repository<Appointment>()
            .GetAsync(new AppointmentWithIntakeSpecification(query.AppointmentId), ct);

        if (results.Count == 0)
            return null;

        var (isOpen, reason) = IntakeWindowService.Evaluate(results[0]);
        return new IntakeWindowResult(isOpen, reason);
    }
}
