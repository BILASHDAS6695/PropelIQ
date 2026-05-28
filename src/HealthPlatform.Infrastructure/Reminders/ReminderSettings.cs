namespace HealthPlatform.Infrastructure.Reminders;

/// <summary>
/// Admin-configurable reminder intervals bound from the "Reminders" section of
/// appsettings.json.  Both values default to the story's required intervals.
/// </summary>
public sealed class ReminderSettings
{
    public const string SectionName = "Reminders";

    /// <summary>Hours before the appointment at which the first reminder fires (default: 24).</summary>
    public int HoursBeforeFirst  { get; init; } = 24;

    /// <summary>Hours before the appointment at which the second reminder fires (default: 2).</summary>
    public int HoursBeforeSecond { get; init; } = 2;

    /// <summary>Base URL of the Angular frontend, used to build intake deep-links in reminder emails.</summary>
    public string FrontendBaseUrl { get; init; } = "https://localhost:4200";
}
