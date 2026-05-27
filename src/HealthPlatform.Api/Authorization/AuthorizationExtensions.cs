using HealthPlatform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace HealthPlatform.Api.Authorization;

/// <summary>
/// Centralized registration of all authorization policies.
/// Call <see cref="AddAuthorizationPolicies"/> from <c>Program.cs</c> — do not
/// scatter policy definitions across controllers or other startup files.
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Registers the three role-scoped authorization policies and the ownership policy
    /// used across the API:
    /// <list type="bullet">
    ///   <item><term>PatientPolicy</term><description>Patient, Staff, Admin</description></item>
    ///   <item><term>StaffPolicy</term><description>Staff, Admin</description></item>
    ///   <item><term>AdminPolicy</term><description>Admin only</description></item>
    ///   <item><term>PatientOwnershipPolicy</term><description>Patient (own data), Staff, Admin</description></item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddAuthorizationPolicies(
        this IServiceCollection services)
    {
        // Stateless singleton — reads only JWT claims and route values, no I/O.
        services.AddSingleton<IAuthorizationHandler, PatientOwnershipHandler>();

        services.AddAuthorization(options =>
        {
            // Any authenticated user (all three roles) may access patient-scoped endpoints.
            options.AddPolicy(PolicyNames.Patient, policy =>
                policy.RequireRole(
                    nameof(UserRole.Patient),
                    nameof(UserRole.Staff),
                    nameof(UserRole.Admin)));

            // Staff and above may access staff-scoped endpoints.
            options.AddPolicy(PolicyNames.Staff, policy =>
                policy.RequireRole(
                    nameof(UserRole.Staff),
                    nameof(UserRole.Admin)));

            // Admin-only endpoints.
            options.AddPolicy(PolicyNames.Admin, policy =>
                policy.RequireRole(nameof(UserRole.Admin)));

            // Ownership policy: role check + resource-level ownership enforcement.
            // Apply to endpoints where a patient may only access their own resource
            // (matched via the {patientId} route parameter).
            options.AddPolicy(PolicyNames.PatientOwnership, policy =>
            {
                policy.RequireRole(
                    nameof(UserRole.Patient),
                    nameof(UserRole.Staff),
                    nameof(UserRole.Admin));
                policy.AddRequirements(new PatientOwnershipRequirement());
            });
        });

        return services;
    }
}

/// <summary>
/// Strongly-typed policy name constants.
/// Use these instead of inline strings in <c>[Authorize(Policy = "...")]</c> attributes.
/// </summary>
public static class PolicyNames
{
    public const string Patient          = "PatientPolicy";
    public const string Staff            = "StaffPolicy";
    public const string Admin            = "AdminPolicy";
    public const string PatientOwnership = "PatientOwnershipPolicy";
}
