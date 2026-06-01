using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HealthPlatform.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthPlatform.Api.Middleware;

internal sealed class SessionValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SessionValidationMiddleware> _logger;

    public SessionValidationMiddleware(
        RequestDelegate next,
        ILogger<SessionValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ISessionStore sessionStore)
    {
        if (ShouldSkip(context))
        {
            await _next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var sub = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(sub, out var userId))
        {
            await WriteSessionExpiredAsync(context);
            return;
        }

        var tokenSid = context.User.FindFirst("sid")?.Value;
        Guid? sidFromToken = null;

        try
        {
            var session = await sessionStore.GetSessionAsync(userId, context.RequestAborted);
            if (session is null)
            {
                _logger.LogInformation(
                    "Session missing or expired for user {UserId} on {Path}",
                    userId,
                    context.Request.Path);
                await WriteSessionExpiredAsync(context);
                return;
            }

            if (!string.IsNullOrWhiteSpace(tokenSid))
            {
                if (!Guid.TryParse(tokenSid, out var parsedSid))
                {
                    _logger.LogInformation(
                        "Invalid sid claim for user {UserId} on {Path}",
                        userId,
                        context.Request.Path);
                    await WriteSessionExpiredAsync(context);
                    return;
                }

                sidFromToken = parsedSid;

                if (sidFromToken.Value != session.SessionId)
                {
                    _logger.LogInformation(
                        "Session sid mismatch for user {UserId} on {Path}",
                        userId,
                        context.Request.Path);
                    await WriteSessionExpiredAsync(context);
                    return;
                }
            }

            await sessionStore.RefreshActivityAsync(
                userId,
                DateTimeOffset.UtcNow,
                context.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Session validation failed for user {UserId} on {Path}",
                userId,
                context.Request.Path);
            await WriteSessionExpiredAsync(context);
            return;
        }

        await _next(context);
    }

    private static bool ShouldSkip(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            return true;

        var path = context.Request.Path;

        return path.StartsWithSegments("/api/auth/login")
            || path.StartsWithSegments("/api/auth/register")
            || path.StartsWithSegments("/api/auth/refresh")
            || path.StartsWithSegments("/api/auth/logout")
            || path.StartsWithSegments("/health")
            || path.StartsWithSegments("/swagger")
            || path.StartsWithSegments("/hubs");
    }

    private static async Task WriteSessionExpiredAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = "Session expired"
        };

        await context.Response.WriteAsJsonAsync(problem);
    }
}
