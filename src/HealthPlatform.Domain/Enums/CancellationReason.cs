namespace HealthPlatform.Domain.Enums;

/// <summary>
/// Reason a patient or staff member provided when cancelling or rescheduling
/// an appointment.  Matches the dropdown values shown in the UI.
/// </summary>
public enum CancellationReason
{
    ScheduleConflict = 0,
    FeelingBetter    = 1,
    Other            = 2
}
