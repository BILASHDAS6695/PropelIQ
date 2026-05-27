using HealthPlatform.Api.Authorization;
using HealthPlatform.Application.Features.Admin;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthPlatform.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = PolicyNames.Admin)]
public sealed class AdminUsersController : ControllerBase
{
    private readonly ISender _sender;

    public AdminUsersController(ISender sender) => _sender = sender;

    /// <summary>
    /// Deactivates a user account and immediately revokes active session artifacts.
    /// </summary>
    /// <param name="userId">User ID to deactivate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 No Content — user deactivated (or already inactive) and tokens revoked.<br/>
    /// 404 Not Found — user does not exist.
    /// </returns>
    [HttpPost("{userId:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateUser(Guid userId, CancellationToken ct)
    {
        var result = await _sender.Send(new DeactivateUserCommand(userId), ct);

        if (!result.IsSuccess)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "User not found.",
                Detail = result.Error
            });
        }

        return NoContent();
    }
}
