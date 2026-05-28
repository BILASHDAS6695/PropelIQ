using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Notifications;

/// <summary>
/// Retrieves a paged list of notifications for a user, optionally filtered to unread only.
/// Use the single-parameter constructor for count queries (no paging).
/// </summary>
public sealed class UserNotificationsSpecification : ISpecification<Notification>
{
    private readonly Guid _userId;
    private readonly bool _unreadOnly;
    private readonly int  _page;
    private readonly int  _pageSize;

    /// <summary>Paged list constructor.</summary>
    public UserNotificationsSpecification(Guid userId, int page, int pageSize, bool unreadOnly)
    {
        _userId     = userId;
        _page       = page;
        _pageSize   = pageSize;
        _unreadOnly = unreadOnly;
        IsPagingEnabled = true;
    }

    /// <summary>Count-only constructor (no paging applied).</summary>
    public UserNotificationsSpecification(Guid userId, bool unreadOnly)
    {
        _userId     = userId;
        _unreadOnly = unreadOnly;
        IsPagingEnabled = false;
    }

    public Expression<Func<Notification, bool>>? Criteria =>
        _unreadOnly
            ? n => n.UserId == _userId && !n.IsRead
            : n => n.UserId == _userId;

    public List<Expression<Func<Notification, object>>> Includes => [];

    public Expression<Func<Notification, object>>? OrderBy           => null;
    public Expression<Func<Notification, object>>? OrderByDescending => n => n.SentAt;

    public bool IsPagingEnabled { get; }
    public int  Skip            => (_page - 1) * _pageSize;
    public int  Take            => _pageSize;
}
