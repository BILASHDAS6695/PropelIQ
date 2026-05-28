using MediatR;

namespace HealthPlatform.Application.Features.Notifications;

public sealed record GetNotificationsQuery(
    int  Page       = 1,
    int  PageSize   = 20,
    bool UnreadOnly = false)
    : IRequest<NotificationsPageDto>;

public sealed record NotificationsPageDto(
    IReadOnlyList<NotificationDto> Items,
    int                            TotalCount,
    int                            UnreadCount,
    int                            Page,
    int                            PageSize);
