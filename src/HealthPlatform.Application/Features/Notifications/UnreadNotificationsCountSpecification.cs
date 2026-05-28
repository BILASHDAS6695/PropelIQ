using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Notifications;

/// <summary>
/// Count-only specification: matches all unread in-app notifications for the given user.
/// </summary>
public sealed class UnreadNotificationsCountSpecification : ISpecification<Notification>
{
    private readonly Guid _userId;

    public UnreadNotificationsCountSpecification(Guid userId) => _userId = userId;

    public Expression<Func<Notification, bool>>? Criteria =>
        n => n.UserId == _userId && !n.IsRead;

    public List<Expression<Func<Notification, object>>> Includes => [];

    public Expression<Func<Notification, object>>? OrderBy           => null;
    public Expression<Func<Notification, object>>? OrderByDescending => null;

    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
