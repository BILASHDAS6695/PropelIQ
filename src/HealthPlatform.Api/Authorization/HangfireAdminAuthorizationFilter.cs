using Hangfire.Dashboard;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Api.Authorization;

/// <summary>
/// Restricts the Hangfire dashboard to authenticated users with the Admin role.
/// Hangfire's <see cref="IDashboardAuthorizationFilter"/> runs outside of
/// ASP.NET Core's normal authorization pipeline, so we inspect the
/// <see cref="HttpContext"/> directly.
/// </summary>
public sealed class HangfireAdminAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true
            && httpContext.User.IsInRole(nameof(UserRole.Admin));
    }
}
