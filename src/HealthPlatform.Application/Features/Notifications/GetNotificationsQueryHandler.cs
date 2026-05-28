using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.Notifications;

internal sealed class GetNotificationsQueryHandler
    : IRequestHandler<GetNotificationsQuery, NotificationsPageDto>
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;

    public GetNotificationsQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow         = uow;
        _currentUser = currentUser;
    }

    public async Task<NotificationsPageDto> Handle(
        GetNotificationsQuery query,
        CancellationToken     ct)
    {
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("User must be authenticated to view notifications.");

        var pagedSpec = new UserNotificationsSpecification(
            userId, query.Page, query.PageSize, query.UnreadOnly);
        var notifications = await _uow.Repository<Notification>().GetAsync(pagedSpec, ct);

        var totalSpec  = new UserNotificationsSpecification(userId, query.UnreadOnly);
        var totalCount = await _uow.Repository<Notification>().CountAsync(totalSpec, ct);

        var unreadCount = await _uow.Repository<Notification>()
            .CountAsync(new UnreadNotificationsCountSpecification(userId), ct);

        var items = notifications
            .Select(n => new NotificationDto(
                n.Id,
                n.Type.ToString(),
                n.Channel.ToString(),
                n.Title,
                n.Message,
                n.ActionUrl,
                n.IsRead,
                n.ReadAt,
                n.SentAt,
                n.ExpiresAt,
                n.AppointmentId))
            .ToList();

        return new NotificationsPageDto(items, totalCount, unreadCount, query.Page, query.PageSize);
    }
}
