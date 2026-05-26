using HealthPlatform.Application.Features.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthPlatform.Api.Controllers;

/// <summary>Authentication and account management endpoints.</summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController : ControllerBase
{
    private readonly ISender _sender;

    public AuthController(ISender sender) => _sender = sender;

    /// <summary>
    /// Registers a new patient account.
    /// </summary>
    /// <param name="request">Registration payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 Created — <c>{ userId }</c> of the newly created account.<br/>
    /// 409 Conflict — the email address is already registered.<br/>
    /// 422 Unprocessable Entity — one or more field validations failed.
    /// </returns>
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse),          StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails),            StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ValidationProblemDetails),  StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken ct)
    {
        var command = new RegisterPatientCommand(
            request.Email,
            request.FirstName,
            request.LastName,
            request.Phone,
            request.Password,
            request.ConfirmPassword);

        var result = await _sender.Send(command, ct);

        if (!result.IsSuccess)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title  = "Registration failed.",
                Detail = result.Error,
            });
        }

        var response = new RegisterResponse(result.UserId!.Value);
        return CreatedAtAction(nameof(Register), response);
    }
}

// ── Request / Response DTOs ──────────────────────────────────────────────────

/// <summary>Payload for POST /api/auth/register.</summary>
public sealed record RegisterRequest(
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    string Password,
    string ConfirmPassword
);

/// <summary>Successful registration response — contains only the new user's ID.</summary>
public sealed record RegisterResponse(Guid UserId);
