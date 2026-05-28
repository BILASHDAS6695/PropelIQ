using MediatR;

namespace HealthPlatform.Application.Features.NotificationPreferences;

public sealed record GetNotificationPreferencesQuery(Guid UserId)
    : IRequest<NotificationPreferencesDto>;
