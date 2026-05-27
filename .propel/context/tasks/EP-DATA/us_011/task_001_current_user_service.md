# Task 001: ICurrentUserService — Authenticated User Context for Audit

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-011 |
| **Epic** | EP-DATA |
| **Layer** | Application (interface) + API (implementation) |
| **Priority** | Critical |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | US-007 (JWT Bearer auth must be configured) |

## Objective

The audit interceptor (Task 002) needs the authenticated user's ID at the
point of every `SaveChanges` call. Define a thin `ICurrentUserService`
abstraction in the Application layer and implement it in the API project using
`IHttpContextAccessor`, keeping Infrastructure free of ASP.NET Core HTTP
concerns.

## Acceptance Criteria Covered

- AC-8: Audit entries include the authenticated user's ID from the request context

## Implementation Steps

### 1. Create `ICurrentUserService` in Application Layer

Create `src/HealthPlatform.Application/Interfaces/ICurrentUserService.cs`:

```csharp
namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Provides the identity of the currently authenticated user.
/// Returns <c>null</c> for unauthenticated contexts (e.g., startup seeding,
/// background services) — callers must check <see cref="IsAuthenticated"/>
/// before consuming <see cref="UserId"/>.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId        { get; }
    bool  IsAuthenticated { get; }
}
```

### 2. Create `HttpCurrentUserService` in API Layer

Create `src/HealthPlatform.Api/Services/HttpCurrentUserService.cs`:

```csharp
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
```

### 3. Register in `Program.cs`

Add after `builder.Services.AddInfrastructure(builder.Configuration)`:

```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, HttpCurrentUserService>();
```

Add using at the top of `Program.cs`:

```csharp
using HealthPlatform.Api.Services;
using HealthPlatform.Application.Interfaces;
```

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Application/Interfaces/ICurrentUserService.cs` | New — user context interface |
| `src/HealthPlatform.Api/Services/HttpCurrentUserService.cs` | New — JWT claim reader |
| `src/HealthPlatform.Api/Program.cs` | Register `IHttpContextAccessor` + `ICurrentUserService` |

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Release
dotnet test HealthPlatform.sln --no-build --configuration Release
```

## Notes

- `IHttpContextAccessor` is already pulled in transitively via ASP.NET Core but
  must be explicitly registered with `AddHttpContextAccessor()` to be resolvable
  from DI.
- `internal sealed` on `HttpCurrentUserService` enforces the Clean Architecture
  boundary — no code outside the API project should reference the concrete type.
- `Guid.TryParse` is used defensively; malformed or missing `NameIdentifier`
  claims return `null` rather than throwing.
- Background services and EF seeding operations have no HTTP context, so
  `IsAuthenticated` returns `false` and the audit interceptor will skip logging
  for those operations (see Task 002).
