using MediatR;

namespace HealthPlatform.Application.Features.NotificationPreferences;

public sealed record UpdateNotificationPreferencesCommand(
    Guid UserId,
    bool EmailReminders,
    bool EmailSwap,
    bool EmailGeneral,
    bool InAppReminders,
    bool InAppSwap,
    bool InAppGeneral) : IRequest;
