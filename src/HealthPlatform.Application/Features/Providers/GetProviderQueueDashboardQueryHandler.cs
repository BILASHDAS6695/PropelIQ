using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Providers;

internal sealed class GetProviderQueueDashboardQueryHandler
    : IRequestHandler<GetProviderQueueDashboardQuery, QueueDashboardDto>
{
    private readonly IUnitOfWork _uow;

    public GetProviderQueueDashboardQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<QueueDashboardDto> Handle(
        GetProviderQueueDashboardQuery query,
        CancellationToken              ct)
    {
        var appointments = await _uow.Repository<Appointment>()
            .GetAsync(new ProviderQueueByDateSpecification(query.ProviderId, query.Date), ct);

        // ── Multi-key sort (in-memory) ────────────────────────────────────
        // Priority group: InProgress(0) > Arrived(1) > Scheduled/Booked(2) > WalkIn(3)
        var sorted = appointments
            .OrderBy(a => a.Status switch
            {
                AppointmentStatus.InProgress => 0,
                AppointmentStatus.Arrived    => 1,
                AppointmentStatus.WalkIn     => 3,
                _                            => 2   // Scheduled, Booked
            })
            .ThenBy(a => a.Status is AppointmentStatus.InProgress or AppointmentStatus.Arrived
                ? a.ArrivalTime ?? DateTimeOffset.MaxValue
                : a.Status == AppointmentStatus.WalkIn
                    ? DateTimeOffset.MinValue.AddMinutes(a.QueuePosition ?? int.MaxValue)
                    : a.SlotTime)
            .ToList();

        var items = sorted
            .Select(a => new QueueEntryDto(
                a.Id,
                a.PatientId,
                $"{a.Patient.FirstName} {a.Patient.LastName}",
                a.Status.ToString(),
                a.IsWalkIn ? (a.ArrivalTime ?? a.SlotTime) : a.SlotTime,
                a.QueuePosition,
                a.VisitReason,
                a.IsWalkIn,
                a.ArrivalTime,
                a.ArrivalTime.HasValue && a.ArrivalTime.Value > a.SlotTime.AddMinutes(15)))
            .ToList();

        var summary = new QueueSummaryDto(
            Waiting:    appointments.Count(a => a.Status == AppointmentStatus.Arrived),
            InProgress: appointments.Count(a => a.Status == AppointmentStatus.InProgress),
            Remaining:  appointments.Count(a =>
                a.Status is AppointmentStatus.Scheduled
                         or AppointmentStatus.Booked
                         or AppointmentStatus.WalkIn));

        return new QueueDashboardDto(items, summary);
    }
}
