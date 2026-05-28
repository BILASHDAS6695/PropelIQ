using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Notifications;

/// <summary>
/// Returns all unread notifications for the given user with no paging.
/// Used by <c>MarkNotificationsReadCommandHandler</c> when marking all as read.
/// </summary>
public sealed class AllUnreadNotificationsSpecification : ISpecification<Notification>
{
    private readonly Guid _userId;

    public AllUnreadNotificationsSpecification(Guid userId) => _userId = userId;

    public Expression<Func<Notification, bool>>? Criteria =>
        n => n.UserId == _userId && !n.IsRead;

    public List<Expression<Func<Notification, object>>> Includes => [];

    public Expression<Func<Notification, object>>? OrderBy           => null;
    public Expression<Func<Notification, object>>? OrderByDescending => null;

    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
