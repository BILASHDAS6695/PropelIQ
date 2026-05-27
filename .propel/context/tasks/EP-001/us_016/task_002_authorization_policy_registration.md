# Task 002: Centralized Authorization Policy Registration

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-016 |
| **Epic** | EP-001 |
| **Layer** | API (DI Registration) |
| **Priority** | Critical |
| **Estimated Effort** | 25 minutes |
| **Dependencies** | None |

## Objective

Replace the bare `builder.Services.AddAuthorization()` call in `Program.cs` with a
**centralized policy registration** that defines all three authorization policies in a
single, discoverable location:

| Policy | Permitted Roles |
|--------|----------------|
| `PatientPolicy` | Patient, Staff, Admin |
| `StaffPolicy` | Staff, Admin |
| `AdminPolicy` | Admin |

The hierarchy ensures that staff endpoints remain accessible to admins and that patient-
level endpoints remain accessible to all authenticated roles, satisfying the inclusive
access model described in the user story.

Policy configuration is extracted into an `AuthorizationExtensions` class so `Program.cs`
stays lean and the policy definitions live in one place (AC: "centralized in a single
registration method").

## Acceptance Criteria Covered

- AC: Three authorization policies defined: `PatientPolicy`, `StaffPolicy`, `AdminPolicy`
- AC: Policy configuration centralized in a single registration method
- AC: Admin endpoints — all staff + user management
- AC: Staff endpoints — all patient + walk-in, queue, arrival
- AC: Patient endpoints — booking, intake, document upload, profile (own)

## Files to Create / Modify

| File | Change |
|------|--------|
| `src/HealthPlatform.Api/Authorization/AuthorizationExtensions.cs` | **Create** — centralized policy definitions |
| `src/HealthPlatform.Api/Program.cs` | **Modify** — replace `AddAuthorization()` with `AddAuthorizationPolicies()` |

---

## Implementation Steps

### 1. Create `AuthorizationExtensions`

**File:** `src/HealthPlatform.Api/Authorization/AuthorizationExtensions.cs`

```csharp
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
    /// Registers the three role-scoped authorization policies used across the API:
    /// <list type="bullet">
    ///   <item><term>PatientPolicy</term><description>Patient, Staff, Admin</description></item>
    ///   <item><term>StaffPolicy</term><description>Staff, Admin</description></item>
    ///   <item><term>AdminPolicy</term><description>Admin only</description></item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddAuthorizationPolicies(
        this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // Any authenticated user (all three roles) may access patient-scoped endpoints.
            // Ownership enforcement for patient-specific resources is handled separately
            // by PatientOwnershipHandler (Task 003).
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
        });

        return services;
    }
}
```

### 2. Create `PolicyNames` constants class

Add to the same file (or a sibling file) to avoid magic strings in controller attributes:

```csharp
namespace HealthPlatform.Api.Authorization;

/// <summary>
/// Strongly-typed policy name constants.
/// Use these instead of inline strings in <c>[Authorize(Policy = "...")]</c> attributes.
/// </summary>
public static class PolicyNames
{
    public const string Patient = "PatientPolicy";
    public const string Staff   = "StaffPolicy";
    public const string Admin   = "AdminPolicy";
}
```

Place `PolicyNames` directly beneath `AuthorizationExtensions` in the same file
`AuthorizationExtensions.cs`.

### 3. Update `Program.cs`

**File:** `src/HealthPlatform.Api/Program.cs`

Add the using directive:

```csharp
using HealthPlatform.Api.Authorization;
```

Locate the existing line:

```csharp
builder.Services.AddAuthorization();
```

Replace with:

```csharp
builder.Services.AddAuthorizationPolicies();
```

No other changes to `Program.cs` are required.

---

## Design Notes

- `RequireRole` matches the `ClaimTypes.Role` claim value. The `JwtTokenService`
  already serialises `UserRole` enum names (`"Patient"`, `"Staff"`, `"Admin"`) as the
  role claim — the `nameof()` expressions produce exactly those strings.
- Using `nameof(UserRole.X)` instead of string literals prevents silent drift if the
  enum member is renamed.
- `PatientPolicy` intentionally includes Staff and Admin because staff must be able to
  access patient-level endpoints (e.g., reading a patient's intake form on their behalf).
  Resource-level scoping (patients cannot access *other* patients' data) is enforced by
  the ownership handler in Task 003 — it is a separate concern from role-based policy.
- `PolicyNames` constants must be referenced by controllers (Task 004) rather than
  repeating the string literals, keeping all policy names in one file.

## Acceptance Checklist

- [ ] `AuthorizationExtensions.cs` created in `src/HealthPlatform.Api/Authorization/`
- [ ] `PolicyNames` static class defined with `Patient`, `Staff`, `Admin` constants
- [ ] Three policies (`PatientPolicy`, `StaffPolicy`, `AdminPolicy`) registered with correct role sets
- [ ] `Program.cs` calls `AddAuthorizationPolicies()` instead of `AddAuthorization()`
- [ ] `using HealthPlatform.Api.Authorization;` added to `Program.cs`
- [ ] Solution builds with 0 errors
