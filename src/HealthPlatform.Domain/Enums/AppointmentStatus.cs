namespace HealthPlatform.Domain.Enums;

public enum AppointmentStatus
{
    Scheduled  = 0,   // Initial state: booked online, not yet checked in
    Booked     = 1,   // Confirmed / checked in at clinic
    Arrived    = 2,
    Completed  = 3,
    Cancelled  = 4,
    NoShow     = 5,
    WalkIn     = 6,   // Unscheduled walk-in; uses QueuePosition instead of SlotId
    InProgress = 7    // Provider has started the consultation
}
