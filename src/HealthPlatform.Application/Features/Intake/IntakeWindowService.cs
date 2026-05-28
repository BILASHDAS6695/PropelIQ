using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.Intake;

/// <summary>
/// Encodes the intake availability window business rule.
/// Open: SlotTime − 7 days ≤ now ≤ ArrivalTime + 15 min (or SlotTime + 1 hr when not yet arrived).
/// Closed after appointment reaches a terminal status or intake is already completed.
/// </summary>
public static class IntakeWindowService
{
    private static readonly TimeSpan PreWindowDays    = TimeSpan.FromDays(7);
    private static readonly TimeSpan PostArrivalMins  = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan PostSlotFallback = TimeSpan.FromHours(1);

    public static (bool IsOpen, string? Reason) Evaluate(Appointment appointment)
    {
        var now = DateTimeOffset.UtcNow;

        // Terminal appointment states close the window
        if (appointment.Status is AppointmentStatus.Completed
                                or AppointmentStatus.Cancelled
                                or AppointmentStatus.NoShow)
            return (false, "Intake period has ended.");

        // Intake already completed — no need to reopen
        if (appointment.IntakeRecord?.Status is IntakeStatus.Completed
                                             or IntakeStatus.ReviewedByProvider)
            return (false, "Intake already completed.");

        var windowStart = appointment.SlotTime - PreWindowDays;
        if (now < windowStart)
            return (false, $"Intake opens {windowStart:MMM d, yyyy}.");

        var windowEnd = appointment.ArrivalTime.HasValue
            ? appointment.ArrivalTime.Value + PostArrivalMins
            : appointment.SlotTime + PostSlotFallback;

        if (now > windowEnd)
            return (false, "Intake period has ended.");

        return (true, null);
    }
}
