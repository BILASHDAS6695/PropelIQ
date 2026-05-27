namespace HealthPlatform.Domain.Enums;

public enum SlotSwapStatus
{
    Pending         = 0,  // Awaiting target patient response
    Accepted        = 1,  // Target patient accepted (US-029)
    Declined        = 2,  // Target patient declined (US-029)
    Cancelled       = 3,  // Requester cancelled the request
    Expired         = 4,  // No response within 24 hours (US-029)
    StaffApproved   = 5,  // Staff force-approved, bypassing target patient (US-030)
    StaffDeclined   = 6,  // Staff force-declined with mandatory reason (US-030)
    StaffReassigned = 7,  // Staff performed three-way slot reassignment (US-030)
}
