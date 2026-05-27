using System.Security.Claims;
using HealthPlatform.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace HealthPlatform.Api.Services;

/// <summary>
/// Reads the authenticated user's ID from the current HTTP request's JWT
/// claims (<see cref="ClaimTypes.NameIdentifier"/>).
/// Returns <c>null</c> / <c>false</c> for unauthenticated requests.
/// </summary>
internal sealed class HttpCurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUserService(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public Guid? UserId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?
                .User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated
        => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;
}
