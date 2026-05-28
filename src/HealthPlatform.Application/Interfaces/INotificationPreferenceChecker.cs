using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Checks whether a given notification channel + type combination is
/// permitted for the specified user based on their stored preferences.
/// </summary>
/// <remarks>
/// Security notifications (account lockout, credential expiry) are sent
/// directly through <see cref="IEmailSender"/> without a
/// <see cref="NotificationType"/> and therefore bypass this check by design —
/// they are always delivered.
/// </remarks>
public interface INotificationPreferenceChecker
{
    /// <summary>
    /// Returns <c>true</c> when the user has the channel + type combination
    /// enabled (or when no preference record exists — default-open).
    /// </summary>
    Task<bool> IsAllowedAsync(
        Guid                userId,
        NotificationChannel channel,
        NotificationType    type,
        CancellationToken   ct = default);
}
