# Task 004: Apply Authorization Policies to Existing API Controllers and Hubs

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-016 |
| **Epic** | EP-001 |
| **Layer** | API (Controllers + SignalR Hubs) |
| **Priority** | Critical |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | Task 002, Task 003 |

## Objective

Replace ad-hoc `[Authorize(Roles = "...")]` attributes on existing controllers and hubs
with the centralized `PolicyNames` constants from Task 002. This makes all authorization
decisions traceable to a single policy registry and satisfies the acceptance criterion:
"Authorization policies applied via `[Authorize(Policy = "...")]` attributes."

Additionally, document the **policy selection guide** that future controllers must follow
so the authorization pattern is consistently applied as new endpoints are added.

## Acceptance Criteria Covered

- AC: Authorization policies applied via `[Authorize(Policy = "...")]` attributes
- AC: Admin endpoints: user management → 403 for non-Admin
- AC: Staff attempts admin-only endpoint → 403
- AC: Unauthorized access returns 403 Forbidden

## Files to Modify

| File | Change |
|------|--------|
| `src/HealthPlatform.Api/Controllers/AdminUsersController.cs` | Replace `[Authorize(Roles = "Admin")]` with `[Authorize(Policy = PolicyNames.Admin)]` |
| `src/HealthPlatform.Api/Hubs/NotificationHub.cs` | Replace bare `[Authorize]` with `[Authorize(Policy = PolicyNames.Patient)]` |
| `src/HealthPlatform.Api/Authorization/AuthorizationExtensions.cs` | Add `using` reference for `PolicyNames` (already in same namespace, no change needed) |

---

## Implementation Steps

### 1. Update `AdminUsersController`

**File:** `src/HealthPlatform.Api/Controllers/AdminUsersController.cs`

Add the `using` directive for the `PolicyNames` constants:

```csharp
using HealthPlatform.Api.Authorization;
```

Replace the class-level attribute:

```csharp
// Before:
[Authorize(Roles = "Admin")]
public sealed class AdminUsersController : ControllerBase

// After:
[Authorize(Policy = PolicyNames.Admin)]
public sealed class AdminUsersController : ControllerBase
```

Full updated file header:

```csharp
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
```

### 2. Update `NotificationHub`

**File:** `src/HealthPlatform.Api/Hubs/NotificationHub.cs`

Add the `using` directive:

```csharp
using HealthPlatform.Api.Authorization;
```

Replace the class-level attribute:

```csharp
// Before:
[Authorize]
public sealed class NotificationHub : Hub

// After:
[Authorize(Policy = PolicyNames.Patient)]
public sealed class NotificationHub : Hub
```

Rationale: The hub requires any authenticated user (patient subscribing to their own
queue status, staff monitoring provider slots, admin viewing system events). `PatientPolicy`
permits all three roles, which is the correct scope for the notification hub.

### 3. Policy selection guide (for future controllers)

Apply the following decision table when adding new controllers or endpoints:

| Endpoint category | Example | Attribute |
|-------------------|---------|-----------|
| Patient's own resource (booking, intake, profile) | `GET /api/patients/{patientId}/profile` | `[Authorize(Policy = PolicyNames.PatientOwnership)]` |
| Patient resource, no ownership check needed | `POST /api/appointments` | `[Authorize(Policy = PolicyNames.Patient)]` |
| Staff-only operation | `POST /api/queue/walk-in` | `[Authorize(Policy = PolicyNames.Staff)]` |
| Admin-only operation | `POST /api/admin/users/{id}/deactivate` | `[Authorize(Policy = PolicyNames.Admin)]` |
| Publicly accessible | `POST /api/auth/login` | `[AllowAnonymous]` |
| SignalR hub (any authenticated) | `/hubs/notifications` | `[Authorize(Policy = PolicyNames.Patient)]` |

The `PolicyNames` constants ensure that this table is enforced — never use raw string
literals or `[Authorize(Roles = "...")]` in new controllers.

---

## Design Notes

- `[Authorize(Roles = "Admin")]` was the original guard on `AdminUsersController`. While
  functionally equivalent for the current case, role-based attributes bypass the
  centralized policy registry and cannot be extended with additional requirements
  (e.g., MFA checks) without touching every controller. Policy-based authorization
  decouples the "what" (controller attribute) from the "how" (policy definition).
- `NotificationHub` previously used bare `[Authorize]`, which requires only
  authentication (any valid JWT). Replacing with `PolicyNames.Patient` adds an explicit
  role constraint while preserving the same effective access since all three roles are
  included — the change is defensive and documents intent.
- The `AuthController` endpoints (`/register`, `/login`, `/refresh`, `/logout`) already
  use `[AllowAnonymous]` or no attribute — they are out of scope for this task.
- Do not apply `[Authorize]` at the `Program.cs` level via `app.MapControllers()
  .RequireAuthorization(...)`. Prefer per-controller attributes for explicitness and
  OpenAPI documentation accuracy.

## Acceptance Checklist

- [ ] `AdminUsersController` uses `[Authorize(Policy = PolicyNames.Admin)]`
- [ ] `NotificationHub` uses `[Authorize(Policy = PolicyNames.Patient)]`
- [ ] `using HealthPlatform.Api.Authorization;` added to both files
- [ ] No remaining `[Authorize(Roles = "...")]` attributes in the codebase
- [ ] Solution builds with 0 errors
- [ ] Manual test: Admin JWT → `POST /api/admin/users/{id}/deactivate` → 204
- [ ] Manual test: Patient JWT → `POST /api/admin/users/{id}/deactivate` → 403
- [ ] Manual test: Staff JWT → `POST /api/admin/users/{id}/deactivate` → 403
