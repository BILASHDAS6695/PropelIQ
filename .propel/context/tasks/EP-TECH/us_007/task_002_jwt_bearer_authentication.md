# Task 002: JWT Bearer Authentication Service Registration

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-007 |
| **Epic** | EP-TECH |
| **Layer** | API / Cross-cutting |
| **Priority** | High |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | None (parallel to Task 001) |

## Objective

Add JWT Bearer authentication infrastructure so that:

1. The `[Authorize]` attribute on the `NotificationHub` (Task 003) and any
   future protected controllers is enforced.
2. The **Authorize** button in Swagger UI (Task 001) passes tokens to
   protected endpoints.
3. Token validation parameters (issuer, audience, signing key) are
   configuration-driven and can be overridden per environment.

## Acceptance Criteria Covered

- AC-3 (backend): Authorize button in Swagger UI requires a valid Bearer token
- AC-6: SignalR authentication validates JWT tokens (authentication service layer)

## Implementation Steps

### 1. Add NuGet Package to `HealthPlatform.Api.csproj`

```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.11" />
```

### 2. Add JWT Configuration Section to `appsettings.json`

```json
"JwtSettings": {
  "Issuer":   "HealthPlatformApi",
  "Audience": "HealthPlatformClients",
  "SecretKey": "CHANGE-ME-USE-A-SECRET-MANAGER-IN-PRODUCTION"
}
```

> **Security note**: `SecretKey` must never be committed as a real value. In
> production, supply it via Azure Key Vault / environment variable binding.
> The placeholder signals to operators that injection is required.

### 3. Add Development Override in `appsettings.Development.json`

```json
"JwtSettings": {
  "SecretKey": "dev-only-secret-at-least-32-chars!!"
}
```

This satisfies HMAC-SHA256's minimum 32-byte key requirement during local
development without touching the production template.

### 4. Add `using` Directives to `Program.cs`

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
```

### 5. Register Authentication + JWT Bearer in `Program.cs`

Add after `builder.Services.AddProblemDetails()`:

```csharp
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidateAudience         = true,
        ValidateLifetime         = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer              = jwtSettings["Issuer"],
        ValidAudience            = jwtSettings["Audience"],
        IssuerSigningKey         = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!))
    };

    // Support SignalR: extract token from query string when WebSocket
    // or Server-Sent Events transport is used (browser cannot set headers).
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) &&
                path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization();
```

### 6. Insert `UseAuthentication()` in the Pipeline (`Program.cs`)

Authentication must come **before** `UseAuthorization()`:

```csharp
app.UseAuthentication();   // ← add this line
app.UseAuthorization();
```

## Files Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Api/HealthPlatform.Api.csproj` | Add `Microsoft.AspNetCore.Authentication.JwtBearer 8.0.11` |
| `src/HealthPlatform.Api/Program.cs` | Add auth service registrations + `UseAuthentication()` |
| `src/HealthPlatform.Api/appsettings.json` | Add `JwtSettings` placeholder section |
| `src/HealthPlatform.Api/appsettings.Development.json` | Add `JwtSettings.SecretKey` for local dev |

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Release
dotnet test HealthPlatform.sln --no-build --configuration Release
```

## Notes

- `Microsoft.IdentityModel.Tokens` and `System.IdentityModel.Tokens.Jwt` are
  pulled in transitively by `Microsoft.AspNetCore.Authentication.JwtBearer`.
- `AddAuthorization()` is required alongside `UseAuthorization()` — the existing
  `UseAuthorization()` call in the pipeline will now be properly backed.
- The `OnMessageReceived` handler enables the WebSocket/SSE token flow used by
  the SignalR JavaScript client (`?access_token=<token>`), satisfying AC-6/AC-7.
