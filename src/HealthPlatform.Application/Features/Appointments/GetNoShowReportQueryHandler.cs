using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Handles <see cref="GetNoShowReportQuery"/>.
///
/// Loads all Completed + NoShow appointments in the requested date range,
/// then aggregates in-memory.  Not paginated — reports are expected to
/// cover bounded date ranges (max 90 days enforced by the validator).
/// </summary>
internal sealed class GetNoShowReportQueryHandler
    : IRequestHandler<GetNoShowReportQuery, NoShowReportDto>
{
    private readonly IUnitOfWork _uow;

    public GetNoShowReportQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<NoShowReportDto> Handle(
        GetNoShowReportQuery query,
        CancellationToken    ct)
    {
        var appointments = await _uow.Repository<Appointment>()
            .GetAsync(new NoShowReportSpecification(query.DateFrom, query.DateTo, query.ProviderId), ct);

        var totalAppointments = appointments.Count;
        var totalNoShows      = appointments.Count(a => a.Status == AppointmentStatus.NoShow);
        var overallRate       = totalAppointments == 0
            ? 0.0
            : Math.Round((double)totalNoShows / totalAppointments * 100, 2);

        // ── By Provider ───────────────────────────────────────────────────
        var byProvider = appointments
            .GroupBy(a => a.ProviderId)
            .Select(g =>
            {
                var provider = g.First().Provider;
                var total    = g.Count();
                var noShows  = g.Count(a => a.Status == AppointmentStatus.NoShow);
                var rate     = total == 0 ? 0.0 : Math.Round((double)noShows / total * 100, 2);
                return new NoShowByProviderDto(
                    ProviderId:        g.Key,
                    ProviderName:      provider.Name,
                    TotalAppointments: total,
                    NoShowCount:       noShows,
                    NoShowRate:        rate);
            })
            .OrderByDescending(x => x.NoShowRate)
            .ToList();

        // ── By Day of Week ────────────────────────────────────────────────
        var byDayOfWeek = appointments
            .GroupBy(a => a.SlotTime.DayOfWeek)
            .Select(g =>
            {
                var total   = g.Count();
                var noShows = g.Count(a => a.Status == AppointmentStatus.NoShow);
                var rate    = total == 0 ? 0.0 : Math.Round((double)noShows / total * 100, 2);
                return new NoShowByDayOfWeekDto(
                    DayOfWeek:         g.Key,
                    DayName:           g.Key.ToString(),
                    TotalAppointments: total,
                    NoShowCount:       noShows,
                    NoShowRate:        rate);
            })
            .OrderBy(x => x.DayOfWeek)
            .ToList();

        // ── By Time Slot (hour bucket) ────────────────────────────────────
        var byTimeSlot = appointments
            .GroupBy(a => a.SlotTime.Hour)
            .Select(g =>
            {
                var hour    = g.Key;
                var total   = g.Count();
                var noShows = g.Count(a => a.Status == AppointmentStatus.NoShow);
                var rate    = total == 0 ? 0.0 : Math.Round((double)noShows / total * 100, 2);
                return new NoShowByTimeSlotDto(
                    HourUtc:           hour,
                    SlotLabel:         $"{hour:D2}:00–{hour:D2}:59 UTC",
                    TotalAppointments: total,
                    NoShowCount:       noShows,
                    NoShowRate:        rate);
            })
            .OrderBy(x => x.HourUtc)
            .ToList();

        return new NoShowReportDto(
            ByProvider:        byProvider,
            ByDayOfWeek:       byDayOfWeek,
            ByTimeSlot:        byTimeSlot,
            TotalAppointments: totalAppointments,
            TotalNoShows:      totalNoShows,
            OverallNoShowRate: overallRate);
    }
}
