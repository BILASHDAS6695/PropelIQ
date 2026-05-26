# Task 004: POST /api/auth/register Endpoint (API Layer)

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-013 |
| **Epic** | EP-001 |
| **Layer** | API |
| **Priority** | Critical |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | Task 003 (RegisterPatientCommand wired up in DI) |

## Objective

Expose patient self-registration over HTTP by creating an `AuthController` in
the API project.  The `POST /api/auth/register` endpoint:

- Accepts a JSON body mapped to `RegisterRequest`.
- Dispatches to `RegisterPatientCommand` via MediatR.
- Returns `201 Created` with a `{ userId }` JSON body on success.
- Returns `409 Conflict` when the email is already registered (no exception
  thrown — the handler returns a failure result).
- Returns `422 Unprocessable Entity` (via `ValidationBehavior` →
  `GlobalExceptionHandler`) when field validation fails.
- Is anonymous (`[AllowAnonymous]`) so it is accessible before JWT is issued.

## Acceptance Criteria Covered

- AC-9: API returns `201 Created` with `userId` (no password in response)
- AC-6: Duplicate email → `409 Conflict` with the error message from the handler
- AC-3/AC-2: Validation errors surface as `422` (handled by existing pipeline)

## Implementation Steps

### 1. Create `AuthController`

Create `src/HealthPlatform.Api/Controllers/AuthController.cs`:

```csharp
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
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails),   StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
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
```

> **Security note**: `RegisterResponse` deliberately excludes the password hash,
> role, and any other sensitive fields.  The endpoint is `[AllowAnonymous]` only
> for registration; all other `AuthController` actions must apply `[Authorize]`.

### 2. Verify `GlobalExceptionHandler` Handles `ValidationException`

Open `src/HealthPlatform.Api/Middleware/GlobalExceptionHandler.cs` and confirm
there is a branch that catches `FluentValidation.ValidationException` and maps
it to HTTP 422 with a `ValidationProblemDetails` body.  If that branch is
missing, add:

```csharp
if (exception is FluentValidation.ValidationException validationEx)
{
    context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
    var errors = validationEx.Errors
        .GroupBy(e => e.PropertyName)
        .ToDictionary(
            g => g.Key,
            g => g.Select(e => e.ErrorMessage).ToArray());

    await context.Response.WriteAsJsonAsync(
        new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title  = "One or more validation errors occurred.",
        }, cancellationToken: ct);
    return;
}
```

Also add the required `using` directives:

```csharp
using FluentValidation;
```

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Api/Controllers/AuthController.cs` | New — register endpoint + DTOs |
| `src/HealthPlatform.Api/Middleware/GlobalExceptionHandler.cs` | Add ValidationException → 422 branch (if absent) |

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Debug
# Expect: 0 errors
```

Manual end-to-end via Swagger UI (`/swagger` in Development):

1. **Happy path** — POST `/api/auth/register` with a valid payload:
   ```json
   {
     "email": "alice@example.com",
     "firstName": "Alice",
     "lastName": "Smith",
     "phone": "+14155552671",
     "password": "Secur3P@ssword!",
     "confirmPassword": "Secur3P@ssword!"
   }
   ```
   Expected: `HTTP 201` with body `{ "userId": "<guid>" }`.

2. **Duplicate email** — repeat the same request:
   Expected: `HTTP 409` with `{ "detail": "An account with this email already exists." }`.

3. **Weak password** — send `"password": "weak"`:
   Expected: `HTTP 422` with validation error details.

## Notes

- The `[AllowAnonymous]` attribute is required on the controller (or action)
  because `Program.cs` calls `app.UseAuthentication()` + `app.UseAuthorization()`;
  without it the endpoint would return `401` before the handler runs.
- `ISender` (from MediatR) is preferred over `IMediator` as it exposes only the
  send capability, following the Interface Segregation Principle.
- The `RegisterRequest` DTO lives in the API project (not Application) to avoid
  coupling the API contract to the CQRS command shape.
