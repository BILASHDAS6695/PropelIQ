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

    /// <summary>
    /// Authenticates a user and issues a JWT access token + refresh token.
    /// </summary>
    /// <returns>
    /// 200 OK — token pair with expiresIn.<br/>
    /// 401 Unauthorized — invalid credentials, inactive, or locked account.<br/>
    /// 422 Unprocessable Entity — input validation failed.
    /// </returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthTokenResponse),        StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails),           StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var result = await _sender.Send(
            new LoginCommand(request.Email, request.Password), ct);

        if (!result.IsSuccess)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title  = "Authentication failed.",
                Detail = result.Error
            });
        }

        return Ok(new AuthTokenResponse(
            result.AccessToken!,
            result.RefreshToken!,
            result.ExpiresIn));
    }

    /// <summary>
    /// Issues a new token pair from a valid refresh token (single-use rotation).
    /// </summary>
    /// <returns>
    /// 200 OK — new token pair.<br/>
    /// 401 Unauthorized — refresh token invalid or expired.
    /// </returns>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails),    StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken ct)
    {
        var result = await _sender.Send(
            new RefreshTokenCommand(request.UserId, request.RefreshToken), ct);

        if (!result.IsSuccess)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title  = "Token refresh failed.",
                Detail = result.Error
            });
        }

        return Ok(new AuthTokenResponse(
            result.AccessToken!,
            result.RefreshToken!,
            result.ExpiresIn));
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

/// <summary>Payload for POST /api/auth/login.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Payload for POST /api/auth/refresh.</summary>
public sealed record RefreshRequest(Guid UserId, string RefreshToken);

/// <summary>Successful authentication response.</summary>
public sealed record AuthTokenResponse(
    string AccessToken,
    string RefreshToken,
    int    ExpiresIn);
