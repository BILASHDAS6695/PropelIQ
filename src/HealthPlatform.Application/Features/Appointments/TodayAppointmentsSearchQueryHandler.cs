using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

internal sealed class TodayAppointmentsSearchQueryHandler
    : IRequestHandler<TodayAppointmentsSearchQuery, IReadOnlyList<TodayAppointmentItemDto>>
{
    private readonly IUnitOfWork _uow;

    public TodayAppointmentsSearchQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<TodayAppointmentItemDto>> Handle(
        TodayAppointmentsSearchQuery query,
        CancellationToken            ct)
    {
        var today        = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var appointments = await _uow.Repository<Appointment>()
            .GetAsync(
                new TodayAppointmentsSearchSpecification(
                    query.ProviderId,
                    today,
                    query.PatientNameFragment,
                    query.AppointmentId),
                ct);

        var list = appointments
            .Select(a => new TodayAppointmentItemDto(
                a.Id,
                a.PatientId,
                $"{a.Patient.FirstName} {a.Patient.LastName}",
                a.Status.ToString(),
                a.SlotTime,
                a.IsWalkIn,
                IsLate(a),
                a.ArrivalTime,
                a.IntakeRecord?.Status.ToString()))
            .ToList();

        if (query.HasIntakePending == true)
            list = list.Where(x => x.IntakeStatus is null or "Draft").ToList();

        return list.AsReadOnly();
    }

    private static bool IsLate(Appointment a) =>
        a.ArrivalTime.HasValue && a.ArrivalTime.Value > a.SlotTime.AddMinutes(15);
}
