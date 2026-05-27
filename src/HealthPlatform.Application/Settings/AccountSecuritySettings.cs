namespace HealthPlatform.Application.Settings;

/// <summary>
/// Strongly-typed options for account lockout and password-policy thresholds.
/// Bound from the <c>AccountSecurity</c> section in <c>appsettings.json</c>.
/// </summary>
public sealed class AccountSecuritySettings
{
    public const string SectionName = "AccountSecurity";

    /// <summary>Failed attempts before lockout is applied. Default: 5.</summary>
    public int MaxFailedLoginAttempts { get; set; } = 5;

    /// <summary>Duration of an automatic lockout in minutes. Default: 15.</summary>
    public int LockoutDurationMinutes { get; set; } = 15;

    /// <summary>Days until the current password expires. Default: 90.</summary>
    public int PasswordExpiryDays { get; set; } = 90;

    /// <summary>Number of previous password hashes retained for reuse prevention. Default: 5.</summary>
    public int PasswordHistorySize { get; set; } = 5;
}
