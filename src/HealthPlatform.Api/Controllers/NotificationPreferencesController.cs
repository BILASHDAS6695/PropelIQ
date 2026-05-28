using HealthPlatform.Application.Features.NotificationPreferences;
using HealthPlatform.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthPlatform.Api.Controllers;

[ApiController]
[Route("api/users/{userId:guid}/notification-preferences")]
[Authorize]
public sealed class NotificationPreferencesController : ControllerBase
{
    private readonly IMediator           _mediator;
    private readonly ICurrentUserService _currentUser;

    public NotificationPreferencesController(
        IMediator           mediator,
        ICurrentUserService currentUser)
    {
        _mediator    = mediator;
        _currentUser = currentUser;
    }

    /// <summary>Returns the notification preferences for the specified user.</summary>
    [HttpGet]
    public async Task<ActionResult<NotificationPreferencesDto>> Get(
        Guid userId,
        CancellationToken ct)
    {
        if (_currentUser.UserId != userId)
            return Forbid();

        var result = await _mediator.Send(new GetNotificationPreferencesQuery(userId), ct);
        return Ok(result);
    }

    /// <summary>Replaces the notification preferences for the specified user.</summary>
    [HttpPut]
    public async Task<IActionResult> Put(
        Guid userId,
        [FromBody] NotificationPreferencesDto body,
        CancellationToken ct)
    {
        if (_currentUser.UserId != userId)
            return Forbid();

        await _mediator.Send(new UpdateNotificationPreferencesCommand(
            userId,
            body.EmailReminders,
            body.EmailSwap,
            body.EmailGeneral,
            body.InAppReminders,
            body.InAppSwap,
            body.InAppGeneral), ct);

        return NoContent();
    }
}
