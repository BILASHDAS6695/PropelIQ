namespace HealthPlatform.Domain.Enums;

public enum SlotSwapStatus
{
    Pending   = 0,   // Awaiting target patient response
    Accepted  = 1,   // Target patient accepted (handled by US-029)
    Declined  = 2,   // Target patient declined (handled by US-029)
    Cancelled = 3,   // Requester cancelled the request
    Expired   = 4    // No response within 24 hours
}
