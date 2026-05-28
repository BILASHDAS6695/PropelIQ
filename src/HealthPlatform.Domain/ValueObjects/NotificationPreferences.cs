namespace HealthPlatform.Domain.ValueObjects;

/// <summary>
/// Stores per-channel, per-category notification opt-in flags for a user.
/// All flags default to <c>true</c> (opt-in) when not explicitly set.
/// Serialised as a JSONB column on the <c>users</c> table.
/// </summary>
public sealed class NotificationPreferences
{
    // ── Email channel ─────────────────────────────────────────────────────
    public bool EmailReminders { get; set; } = true;
    public bool EmailSwap      { get; set; } = true;
    public bool EmailGeneral   { get; set; } = true;

    // ── In-app channel ────────────────────────────────────────────────────
    public bool InAppReminders { get; set; } = true;
    public bool InAppSwap      { get; set; } = true;
    public bool InAppGeneral   { get; set; } = true;
}
