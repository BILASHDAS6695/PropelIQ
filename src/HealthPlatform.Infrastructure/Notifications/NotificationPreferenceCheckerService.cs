using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using HealthPlatform.Domain.ValueObjects;

namespace HealthPlatform.Infrastructure.Notifications;

/// <summary>
/// Loads the user's <see cref="NotificationPreferences"/> from the database
/// and answers whether a given channel + notification type is permitted.
/// Defaults to <c>true</c> (allowed) when the user record cannot be loaded.
/// </summary>
internal sealed class NotificationPreferenceCheckerService : INotificationPreferenceChecker
{
    private readonly IUnitOfWork _uow;

    public NotificationPreferenceCheckerService(IUnitOfWork uow)
        => _uow = uow;

    public async Task<bool> IsAllowedAsync(
        Guid                userId,
        NotificationChannel channel,
        NotificationType    type,
        CancellationToken   ct = default)
    {
        var user = await _uow.Repository<User>().GetByIdAsync(userId, ct);
        if (user is null)
            return true; // default-open: do not silently drop notifications for unknown users

        var prefs = user.NotificationPreferences;
        return channel switch
        {
            NotificationChannel.Email => IsEmailAllowed(prefs, type),
            NotificationChannel.InApp => IsInAppAllowed(prefs, type),
            _                         => true, // Sms — not gated by user prefs yet
        };
    }

    private static bool IsEmailAllowed(NotificationPreferences p, NotificationType t) =>
        t switch
        {
            NotificationType.Reminder                              => p.EmailReminders,
            NotificationType.SwapRequest
                or NotificationType.SwapResult
                or NotificationType.SlotSwap                      => p.EmailSwap,
            _                                                      => p.EmailGeneral,
        };

    private static bool IsInAppAllowed(NotificationPreferences p, NotificationType t) =>
        t switch
        {
            NotificationType.Reminder                              => p.InAppReminders,
            NotificationType.SwapRequest
                or NotificationType.SwapResult
                or NotificationType.SlotSwap                      => p.InAppSwap,
            _                                                      => p.InAppGeneral,
        };
}
