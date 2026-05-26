# Task 004: CORS Configuration for SignalR

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-007 |
| **Epic** | EP-TECH |
| **Layer** | API |
| **Priority** | High |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | Task 003 (CORS must be applied before hub is mapped) |

## Objective

Configure a named CORS policy that:

1. Allows only the known Angular frontend origin to connect to the API and
   the SignalR hub.
2. Permits the credentials header required by SignalR's negotiate handshake.
3. Does not expose the API surface to arbitrary origins (prevents CORS
   misconfiguration — OWASP A05:2021).

The allowed origin is externalised to `appsettings.json` so that CI/CD
environments can override it without recompilation.

## Acceptance Criteria Covered

- AC-8: CORS configured for SignalR (frontend origin only)

## Implementation Steps

### 1. Add CORS Origins to `appsettings.json`

```json
"Cors": {
  "AllowedOrigins": [ "http://localhost:4200" ]
}
```

### 2. Add Development Override in `appsettings.Development.json`

```json
"Cors": {
  "AllowedOrigins": [ "http://localhost:4200" ]
}
```

Production deployments should override `AllowedOrigins` via environment
variables or a secrets manager with the real front-end URL.

### 3. Register CORS Service in `Program.cs`

Add after `builder.Services.AddAuthorization()` (before `AddSignalR()`):

```csharp
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());   // required by SignalR negotiate
});
```

> **Security**: `AllowCredentials()` is intentionally combined with
> `WithOrigins(...)` (not `AllowAnyOrigin()`). Using `AllowAnyOrigin()` with
> `AllowCredentials()` is disallowed by the ASP.NET Core CORS middleware and
> would throw at startup — this pattern is therefore safe by design.

### 4. Insert `UseCors()` in the Pipeline (`Program.cs`)

CORS must be applied **before** the SignalR hub negotiation and before any
response-writing middleware. Insert it immediately after
`UseMiddleware<CorrelationIdMiddleware>()`:

```csharp
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseCors("Frontend");          // ← add this line
app.UseExceptionHandler();
// ... rest of pipeline unchanged
```

## Files Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Api/Program.cs` | Add `AddCors()` + `UseCors("Frontend")` |
| `src/HealthPlatform.Api/appsettings.json` | Add `Cors.AllowedOrigins` array |
| `src/HealthPlatform.Api/appsettings.Development.json` | Add `Cors.AllowedOrigins` for local dev |

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Release
dotnet test HealthPlatform.sln --no-build --configuration Release

# Manual check — preflight from the frontend origin should return 200:
# curl -X OPTIONS https://localhost:{port}/hubs/notifications \
#   -H "Origin: http://localhost:4200" \
#   -H "Access-Control-Request-Method: GET" -v
# Expect: Access-Control-Allow-Origin: http://localhost:4200
#         Access-Control-Allow-Credentials: true

# Preflight from a disallowed origin should NOT return Access-Control-Allow-Origin:
# curl -X OPTIONS https://localhost:{port}/hubs/notifications \
#   -H "Origin: http://evil.example.com" \
#   -H "Access-Control-Request-Method: GET" -v
```

## Notes

- `AllowAnyHeader()` and `AllowAnyMethod()` are intentionally permissive for
  the named `"Frontend"` policy; restrict headers/methods further if the
  security posture requires it.
- In production, inject the real frontend CDN/domain via an environment variable:
  `Cors__AllowedOrigins__0=https://app.healthplatform.com`.
- The `"Frontend"` policy name is consistent with the Angular proxy config
  (`proxy.conf.json`) which also uses `localhost:4200`.
- `UseCors()` must appear before `UseAuthentication()`, `UseAuthorization()`,
  `UseExceptionHandler()`, and hub mapping to ensure preflight `OPTIONS` requests
  are handled before any auth or routing middleware short-circuits them.
