namespace HealthPlatform.Application.Features.NotificationPreferences;

public sealed record NotificationPreferencesDto(
    bool EmailReminders,
    bool EmailSwap,
    bool EmailGeneral,
    bool InAppReminders,
    bool InAppSwap,
    bool InAppGeneral);
