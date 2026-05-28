using MediatR;

namespace HealthPlatform.Application.Features.Notifications;

/// <summary>
/// Marks one or all unread notifications as read for the current user.
/// When <see cref="TargetId"/> is <c>null</c> all unread notifications are marked.
/// Returns the count of notifications actually updated.
/// </summary>
public sealed record MarkNotificationsReadCommand(Guid? TargetId = null)
    : IRequest<int>;
