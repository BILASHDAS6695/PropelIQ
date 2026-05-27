using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Returns a no-show analytics report for a date range, aggregating by
/// provider, day of week, and time slot.  Optionally filtered to a single
/// provider via <paramref name="ProviderId"/>.
/// </summary>
public sealed record GetNoShowReportQuery(
    DateOnly DateFrom,
    DateOnly DateTo,
    Guid?    ProviderId = null) : IRequest<NoShowReportDto>;

public sealed record NoShowReportDto(
    IReadOnlyList<NoShowByProviderDto>  ByProvider,
    IReadOnlyList<NoShowByDayOfWeekDto> ByDayOfWeek,
    IReadOnlyList<NoShowByTimeSlotDto>  ByTimeSlot,
    int                                 TotalAppointments,
    int                                 TotalNoShows,
    double                              OverallNoShowRate);

public sealed record NoShowByProviderDto(
    Guid   ProviderId,
    string ProviderName,
    int    TotalAppointments,
    int    NoShowCount,
    double NoShowRate);

public sealed record NoShowByDayOfWeekDto(
    DayOfWeek DayOfWeek,
    string    DayName,
    int       TotalAppointments,
    int       NoShowCount,
    double    NoShowRate);

public sealed record NoShowByTimeSlotDto(
    int    HourUtc,
    string SlotLabel,
    int    TotalAppointments,
    int    NoShowCount,
    double NoShowRate);
