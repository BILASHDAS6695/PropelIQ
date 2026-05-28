namespace HealthPlatform.Domain.Enums;

public enum NotificationType
{
    Reminder,       // 0 — appointment reminder (email + in-app)
    Confirmation,   // 1 — booking/cancellation confirmation (email)
    SlotSwap,       // 2 — legacy; prefer SwapRequest/SwapResult
    General,        // 3

    // ── In-app notification types (US-034) ───────────────────────────────
    SwapRequest,    // 4 — swap request received (high-priority toast)
    SwapResult,     // 5 — swap request accepted/declined
    ArrivalAlert,   // 6 — patient arrived (high-priority toast for staff)
    StatusChange,   // 7 — appointment status changed
}
