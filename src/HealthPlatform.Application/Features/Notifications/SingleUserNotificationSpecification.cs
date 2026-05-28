using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Notifications;

/// <summary>
/// Loads a single notification by ID, scoped to the owning user (ownership guard).
/// </summary>
public sealed class SingleUserNotificationSpecification : ISpecification<Notification>
{
    private readonly Guid _userId;
    private readonly Guid _notificationId;

    public SingleUserNotificationSpecification(Guid userId, Guid notificationId)
    {
        _userId         = userId;
        _notificationId = notificationId;
    }

    public Expression<Func<Notification, bool>>? Criteria =>
        n => n.Id == _notificationId && n.UserId == _userId;

    public List<Expression<Func<Notification, object>>> Includes => [];

    public Expression<Func<Notification, object>>? OrderBy           => null;
    public Expression<Func<Notification, object>>? OrderByDescending => null;

    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
