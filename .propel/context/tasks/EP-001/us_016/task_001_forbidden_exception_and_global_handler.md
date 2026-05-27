# Task 001: ForbiddenAccessException + 403 GlobalExceptionHandler Mapping

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-016 |
| **Epic** | EP-001 |
| **Layer** | Domain + API Middleware |
| **Priority** | Critical |
| **Estimated Effort** | 20 minutes |
| **Dependencies** | None |

## Objective

Introduce a `ForbiddenAccessException` domain exception and wire it into the existing
`GlobalExceptionHandler` so that any authorization violation thrown from within the
Application or Domain layer is surfaced as an RFC 7807 **403 Forbidden** response — not
the currently unmapped 500.

The `UnauthorizedAccessException` mapping (401) already in the handler is **not** the
correct vehicle for role/policy denials; 403 is the semantically correct status for
"authenticated but not permitted" scenarios.

## Acceptance Criteria Covered

- AC: Unauthorized access returns 403 Forbidden
- AC: Patient attempts to access another patient's data → 403
- AC: Staff attempts admin-only endpoint → 403

## Files to Create / Modify

| File | Change |
|------|--------|
| `src/HealthPlatform.Domain/Common/Exceptions/ForbiddenAccessException.cs` | **Create** |
| `src/HealthPlatform.Api/Middleware/GlobalExceptionHandler.cs` | **Modify** — add 403 mapping |

---

## Implementation Steps

### 1. Create `ForbiddenAccessException`

**File:** `src/HealthPlatform.Domain/Common/Exceptions/ForbiddenAccessException.cs`

```csharp
namespace HealthPlatform.Domain.Common.Exceptions;

/// <summary>
/// Thrown when an authenticated user attempts to access a resource or operation
/// that their role or ownership does not permit.
/// Maps to HTTP 403 Forbidden.
/// </summary>
public sealed class ForbiddenAccessException : DomainException
{
    public ForbiddenAccessException()
        : base("You do not have permission to perform this action.") { }

    public ForbiddenAccessException(string message)
        : base(message) { }
}
```

### 2. Update `GlobalExceptionHandler` — add 403 branch

**File:** `src/HealthPlatform.Api/Middleware/GlobalExceptionHandler.cs`

Locate the `exception switch` expression and insert the new `ForbiddenAccessException`
arm **before** the catch-all `_` arm:

```csharp
// Before (existing switch):
var (statusCode, title) = exception switch
{
    NotFoundException           => (StatusCodes.Status404NotFound,              "Resource Not Found"),
    ConflictException           => (StatusCodes.Status409Conflict,              "Conflict"),
    ValidationException         => (StatusCodes.Status422UnprocessableEntity,   "Validation Failed"),
    ArgumentException           => (StatusCodes.Status400BadRequest,            "Bad Request"),
    UnauthorizedAccessException => (StatusCodes.Status401Unauthorized,          "Unauthorized"),
    _                           => (StatusCodes.Status500InternalServerError,   "Internal Server Error")
};

// After (add ForbiddenAccessException arm):
var (statusCode, title) = exception switch
{
    NotFoundException           => (StatusCodes.Status404NotFound,              "Resource Not Found"),
    ConflictException           => (StatusCodes.Status409Conflict,              "Conflict"),
    ValidationException         => (StatusCodes.Status422UnprocessableEntity,   "Validation Failed"),
    ArgumentException           => (StatusCodes.Status400BadRequest,            "Bad Request"),
    ForbiddenAccessException    => (StatusCodes.Status403Forbidden,             "Forbidden"),
    UnauthorizedAccessException => (StatusCodes.Status401Unauthorized,          "Unauthorized"),
    _                           => (StatusCodes.Status500InternalServerError,   "Internal Server Error")
};
```

Add the missing `using` directive at the top of `GlobalExceptionHandler.cs`:

```csharp
using HealthPlatform.Domain.Common.Exceptions;
```

---

## Design Notes

- `ForbiddenAccessException` extends `DomainException` (not `UnauthorizedAccessException`)
  to keep 401 and 403 semantically distinct. 401 = not authenticated; 403 = not authorized.
- The `message` constructor overload allows callers to provide context-specific denial
  reasons that are safe to surface in the response body (no PHI).
- ASP.NET Core's built-in policy enforcement already returns 403 automatically when
  `[Authorize(Policy = "...")]` is applied. This exception path is reserved for
  Application-layer ownership checks (e.g., `PatientOwnershipHandler`) that throw
  rather than rely on the middleware short-circuit.
- Never include `userId` or resource identifiers in the exception message — log them
  separately via structured logging.

## Acceptance Checklist

- [ ] `ForbiddenAccessException.cs` created in `Domain/Common/Exceptions/`
- [ ] `GlobalExceptionHandler` switch includes `ForbiddenAccessException → 403`
- [ ] `using HealthPlatform.Domain.Common.Exceptions;` present in handler file
- [ ] Solution builds with 0 errors
- [ ] `GET /health` still returns 200 (regression check)
