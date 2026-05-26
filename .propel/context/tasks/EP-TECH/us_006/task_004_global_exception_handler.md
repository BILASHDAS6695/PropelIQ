# Task 004: Global Exception Handler & ProblemDetails

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-006 |
| **Epic** | EP-TECH |
| **Layer** | API (middleware) |
| **Priority** | High |
| **Estimated Effort** | 1 hour |
| **Dependencies** | Task 001 (Serilog must be configured before exception logging) |

## Objective

All unhandled exceptions must produce RFC 7807 `ProblemDetails` JSON responses
with appropriate HTTP status codes, logged at `Error` level with full stack
traces. Stack trace detail is never exposed to API consumers in Production.
The implementation uses ASP.NET Core's built-in `IProblemDetailsService` and
`IExceptionHandler` (introduced in .NET 8) — no third-party libraries required.

## Implementation Steps

### 1. Register ProblemDetails Service in `Program.cs`

```csharp
builder.Services.AddProblemDetails();
```

This enables the built-in `IProblemDetailsService` and `ProblemDetailsFactory`
that `UseExceptionHandler` and `UseStatusCodePages` will use automatically.

### 2. Create Domain Exception Hierarchy

**File:** `src/HealthPlatform.Domain/Common/Exceptions/DomainException.cs`

```csharp
namespace HealthPlatform.Domain.Common.Exceptions;

/// <summary>Base type for all domain-layer exceptions.</summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
    protected DomainException(string message, Exception inner) : base(message, inner) { }
}
```

**File:** `src/HealthPlatform.Domain/Common/Exceptions/NotFoundException.cs`

```csharp
namespace HealthPlatform.Domain.Common.Exceptions;

/// <summary>Thrown when a requested aggregate root is not found.</summary>
public sealed class NotFoundException : DomainException
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} with key '{key}' was not found.") { }
}
```

**File:** `src/HealthPlatform.Domain/Common/Exceptions/ConflictException.cs`

```csharp
namespace HealthPlatform.Domain.Common.Exceptions;

/// <summary>Thrown when an operation would create a conflicting state.</summary>
public sealed class ConflictException : DomainException
{
    public ConflictException(string message) : base(message) { }
}
```

### 3. Create Global Exception Handler

**File:** `src/HealthPlatform.Api/Middleware/GlobalExceptionHandler.cs`

```csharp
using HealthPlatform.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace HealthPlatform.Api.Middleware;

/// <summary>
/// IExceptionHandler implementation registered with UseExceptionHandler().
/// Maps domain exceptions to RFC 7807 ProblemDetails responses.
/// Stack traces are included only in Development.
/// </summary>
internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            NotFoundException       => (StatusCodes.Status404NotFound,       "Resource Not Found"),
            ConflictException       => (StatusCodes.Status409Conflict,        "Conflict"),
            ArgumentException       => (StatusCodes.Status400BadRequest,      "Bad Request"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            _                       => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        _logger.LogError(
            exception,
            "Unhandled exception {ExceptionType}: {Message}",
            exception.GetType().Name,
            exception.Message);

        httpContext.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message,
            Instance = httpContext.Request.Path
        };

        // Only expose stack trace in Development
        if (_environment.IsDevelopment())
        {
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;
        }

        // Propagate correlation ID into error response
        if (httpContext.Request.Headers.TryGetValue("X-Correlation-Id", out var correlationId))
        {
            problemDetails.Extensions["correlationId"] = (string?)correlationId;
        }

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
```

### 4. Register and Wire Middleware in `Program.cs`

```csharp
// Register the handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// --- In the middleware pipeline (before UseAuthorization) ---
app.UseExceptionHandler();
app.UseStatusCodePages();   // Converts bare 4xx/5xx into ProblemDetails
```

**Pipeline order matters.** `UseExceptionHandler()` must come before
`UseRouting()` / `MapControllers()` so it can intercept exceptions thrown by
controller actions.

### 5. Validate FluentValidation Errors Surface as 400 ProblemDetails

The existing `ValidationBehavior<TRequest, TResponse>` in the Application layer
throws `ValidationException`. Extend the exception mapping in `GlobalExceptionHandler`:

```csharp
using FluentValidation;

// Add to the switch expression:
ValidationException ve => (StatusCodes.Status400BadRequest, "Validation Failed"),
```

Add a detail block for validation errors:

```csharp
if (exception is ValidationException validationException)
{
    problemDetails.Extensions["errors"] = validationException.Errors
        .GroupBy(e => e.PropertyName)
        .ToDictionary(
            g => g.Key,
            g => g.Select(e => e.ErrorMessage).ToArray());
}
```

## Acceptance Criteria

- [ ] `DomainException`, `NotFoundException`, `ConflictException` exist in `HealthPlatform.Domain`
- [ ] `GlobalExceptionHandler` implements `IExceptionHandler` and is registered via `AddExceptionHandler<T>()`
- [ ] `UseExceptionHandler()` and `UseStatusCodePages()` called in `Program.cs` pipeline
- [ ] `AddProblemDetails()` registered before `app.Build()`
- [ ] `NotFoundException` → `404`, `ConflictException` → `409`, `ValidationException` → `400`, unhandled → `500`
- [ ] All unhandled exceptions logged at `Error` level with full stack trace via Serilog
- [ ] Stack trace NOT present in response body when `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Stack trace IS present in `extensions.stackTrace` when `ASPNETCORE_ENVIRONMENT=Development`
- [ ] Response `Content-Type` is `application/problem+json`
- [ ] `dotnet build` passes with `TreatWarningsAsErrors=true`

## Verification

```bash
# 1. Start the API in Development mode
# 2. Invoke a non-existent route to get 404 ProblemDetails:
curl -s http://localhost:5013/api/nonexistent | jq .
# Expected: {"status":404,"title":"Resource Not Found",...}

# 3. Verify no stack trace in Production response body:
ASPNETCORE_ENVIRONMENT=Production dotnet run --project src/HealthPlatform.Api
curl -s http://localhost:5013/api/nonexistent | jq '.extensions.stackTrace'
# Expected: null
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| US-006 AC-6 | Global exception handler returns RFC 7807 ProblemDetails |
| US-006 AC-7 | Unhandled exceptions logged at Error with full stack trace |
| TR-019 | Serilog structured logging (exception logging) |
