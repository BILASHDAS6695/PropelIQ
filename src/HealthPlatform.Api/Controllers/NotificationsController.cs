using HealthPlatform.Application.Features.Notifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthPlatform.Api.Controllers;

/// <summary>
/// Provides read access and mark-read actions for the authenticated user's in-app notifications.
/// </summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly ISender _sender;

    public NotificationsController(ISender sender) => _sender = sender;

    /// <summary>
    /// Returns a paginated list of in-app notifications for the authenticated user.
    /// </summary>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page, max 100 (default 20).</param>
    /// <param name="unreadOnly">When <c>true</c> only unread notifications are returned.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK — page of notifications with total and unread counts.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(NotificationsPageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] int  page       = 1,
        [FromQuery] int  pageSize   = 20,
        [FromQuery] bool unreadOnly = false,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetNotificationsQuery(page, pageSize, unreadOnly), ct);
        return Ok(result);
    }

    /// <summary>
    /// Marks one or all unread notifications as read for the authenticated user.
    /// Omit <c>targetId</c> (or set to <c>null</c>) to mark all unread as read.
    /// </summary>
    /// <param name="request">Optional target notification ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK — count of notifications updated.</returns>
    [HttpPost("mark-read")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkRead(
        [FromBody]        MarkReadRequest request,
        CancellationToken ct = default)
    {
        var count = await _sender.Send(new MarkNotificationsReadCommand(request.TargetId), ct);
        return Ok(count);
    }
}

/// <summary>Request body for the mark-read endpoint.</summary>
public sealed record MarkReadRequest(Guid? TargetId);
