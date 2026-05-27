using HealthPlatform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace HealthPlatform.Api.Authorization;

/// <summary>
/// Enforces resource-level ownership for patient-scoped API endpoints.
///
/// Pass criteria (handler succeeds):
///   - Authenticated user's role is Staff or Admin (can access any patient).
///   - Authenticated user's role is Patient AND their <c>sub</c> claim matches the
///     <c>patientId</c> route value (own data only).
///
/// Failure criteria:
///   - Patient user attempts to access a <c>patientId</c> that is not their own.
///     ASP.NET Core translates a handler failure to HTTP 403.
///
/// The handler does <em>not</em> hit the database — it relies solely on JWT claims and
/// route values, keeping authorization logic fast and infrastructure-free.
/// </summary>
internal sealed class PatientOwnershipHandler
    : AuthorizationHandler<PatientOwnershipRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PatientOwnershipRequirement requirement)
    {
        var user = context.User;

        if (!user.Identity?.IsAuthenticated ?? true)
        {
            // Not authenticated — challenge middleware handles 401; handler simply does
            // not succeed to avoid masking the authentication failure.
            return Task.CompletedTask;
        }

        var roleClaimValue = user.FindFirstValue(ClaimTypes.Role);

        // Staff and Admin can access any patient's data — succeed immediately.
        if (roleClaimValue is nameof(UserRole.Staff) or nameof(UserRole.Admin))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Patient users must own the resource.
        if (roleClaimValue == nameof(UserRole.Patient))
        {
            var subjectId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? user.FindFirstValue("sub");

            // Attempt to read patientId from route values (e.g., /api/patients/{patientId}/...).
            var routePatientId = GetRoutePatientId(context);

            if (subjectId is not null
                && routePatientId is not null
                && string.Equals(subjectId, routePatientId, StringComparison.OrdinalIgnoreCase))
            {
                context.Succeed(requirement);
            }
            // If IDs do not match, do not call Succeed → ASP.NET Core returns 403.
        }

        return Task.CompletedTask;
    }

    private static string? GetRoutePatientId(AuthorizationHandlerContext context)
    {
        if (context.Resource is HttpContext httpContext)
        {
            return httpContext.GetRouteValue("patientId")?.ToString();
        }

        return null;
    }
}
