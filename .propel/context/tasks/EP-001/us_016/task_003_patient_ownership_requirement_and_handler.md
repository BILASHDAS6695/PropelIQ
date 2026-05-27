# Task 003: PatientOwnershipRequirement and PatientOwnershipHandler

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-016 |
| **Epic** | EP-001 |
| **Layer** | Application (requirement) + API Authorization (handler) |
| **Priority** | Critical |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | Task 002 |

## Objective

Implement resource-level authorization for patient-scoped endpoints so that:

- A **patient** can only access data belonging to their own account (`sub` claim ==
  resource owner).
- A **staff** or **admin** user can access any patient's data within their permitted scope.

This is expressed as a custom `IAuthorizationRequirement` + `IAuthorizationHandler` pair
registered with ASP.NET Core's policy engine. Controllers enforce ownership by combining
`[Authorize(Policy = "PatientPolicy")]` with the `PatientOwnership` requirement.

## Acceptance Criteria Covered

- AC: Resource-level authorization: patients can only access their own data (ownership check)
- AC: Staff can access any patient's data within their authorized scope
- AC: Patient attempts to access another patient's data → 403
- AC: Deactivated user with valid JWT → session check blocks before authorization

## Files to Create / Modify

| File | Change |
|------|--------|
| `src/HealthPlatform.Application/Authorization/PatientOwnershipRequirement.cs` | **Create** — requirement marker |
| `src/HealthPlatform.Api/Authorization/PatientOwnershipHandler.cs` | **Create** — handler reads HttpContext |
| `src/HealthPlatform.Api/Authorization/AuthorizationExtensions.cs` | **Modify** — register handler + ownership policy |

---

## Implementation Steps

### 1. Create `PatientOwnershipRequirement`

**File:** `src/HealthPlatform.Application/Authorization/PatientOwnershipRequirement.cs`

```csharp
using Microsoft.AspNetCore.Authorization;

namespace HealthPlatform.Application.Authorization;

/// <summary>
/// Requirement that enforces patient data ownership:
/// the authenticated user must be the resource owner (matching <c>patientId</c>
/// route or query parameter) OR hold a Staff / Admin role.
/// </summary>
public sealed class PatientOwnershipRequirement : IAuthorizationRequirement { }
```

### 2. Create `PatientOwnershipHandler`

**File:** `src/HealthPlatform.Api/Authorization/PatientOwnershipHandler.cs`

```csharp
using HealthPlatform.Application.Authorization;
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
        // HttpContext is available when the resource is DefaultHttpContext or
        // the route endpoint's HttpContext via IHttpContextAccessor.
        if (context.Resource is HttpContext httpContext)
        {
            return httpContext.GetRouteValue("patientId")?.ToString();
        }

        return null;
    }
}
```

### 3. Register handler and `PatientOwnershipPolicy` in `AuthorizationExtensions`

**File:** `src/HealthPlatform.Api/Authorization/AuthorizationExtensions.cs`

Extend the existing `AddAuthorizationPolicies` method:

```csharp
using HealthPlatform.Application.Authorization;
using HealthPlatform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace HealthPlatform.Api.Authorization;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddAuthorizationPolicies(
        this IServiceCollection services)
    {
        // Register the ownership handler so DI injects it into the policy engine.
        services.AddSingleton<IAuthorizationHandler, PatientOwnershipHandler>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy(PolicyNames.Patient, policy =>
                policy.RequireRole(
                    nameof(UserRole.Patient),
                    nameof(UserRole.Staff),
                    nameof(UserRole.Admin)));

            options.AddPolicy(PolicyNames.Staff, policy =>
                policy.RequireRole(
                    nameof(UserRole.Staff),
                    nameof(UserRole.Admin)));

            options.AddPolicy(PolicyNames.Admin, policy =>
                policy.RequireRole(nameof(UserRole.Admin)));

            // Ownership policy: PatientPolicy role check + ownership requirement.
            // Apply to endpoints where a patient may only access their own resource.
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
```

Add `PatientOwnership` to the `PolicyNames` constants:

```csharp
public static class PolicyNames
{
    public const string Patient          = "PatientPolicy";
    public const string Staff            = "StaffPolicy";
    public const string Admin            = "AdminPolicy";
    public const string PatientOwnership = "PatientOwnershipPolicy";
}
```

### 4. Usage pattern for future controllers

Controllers that expose patient-scoped resources should use:

```csharp
// A patient may only GET their own profile; staff/admin can GET any.
[HttpGet("{patientId:guid}/profile")]
[Authorize(Policy = PolicyNames.PatientOwnership)]
public async Task<IActionResult> GetProfile(Guid patientId, CancellationToken ct) { ... }
```

The route parameter **must** be named `patientId` for the handler to locate it.

---

## Design Notes

- `PatientOwnershipRequirement` is placed in **Application** (no infrastructure
  dependencies) so it can be referenced from command handlers if needed. The handler
  lives in **API** because it reads `HttpContext` route values — an infrastructure
  concern for the presentation layer.
- `PatientOwnershipHandler` is registered as `Singleton` because it is stateless; all
  contextual data comes from the per-request `AuthorizationHandlerContext`.
- The handler never queries the database. Ownership is determined entirely from the JWT
  `sub` claim vs. the `patientId` route value. This keeps the fast-path authorization
  sub-millisecond.
- If a future endpoint uses a query string parameter (e.g., `?patientId=...`) rather
  than a route value, extend `GetRoutePatientId` to also check
  `httpContext.Request.Query["patientId"]`.
- The `SessionValidationMiddleware` (US-015) already runs before `UseAuthorization()`,
  ensuring deactivated users are blocked before the ownership handler executes.

## Acceptance Checklist

- [ ] `PatientOwnershipRequirement.cs` created in `Application/Authorization/`
- [ ] `PatientOwnershipHandler.cs` created in `Api/Authorization/`
- [ ] Handler registered as `IAuthorizationHandler` singleton in `AddAuthorizationPolicies`
- [ ] `PatientOwnershipPolicy` added to policy options with role + requirement
- [ ] `PolicyNames.PatientOwnership` constant added
- [ ] Solution builds with 0 errors
- [ ] Manual test: patient JWT with `sub != patientId` route → 403
- [ ] Manual test: staff JWT with any `patientId` → passes ownership check
