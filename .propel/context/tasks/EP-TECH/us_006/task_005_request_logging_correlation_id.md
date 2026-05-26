# Task 005: Request/Response Logging & Correlation ID Middleware

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-006 |
| **Epic** | EP-TECH |
| **Layer** | API (middleware) |
| **Priority** | High |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | Task 001 (Serilog), Task 004 (exception handler in correct pipeline position) |

## Objective

Every HTTP request must be logged with method, path, status code, and elapsed
duration. A correlation ID (`X-Correlation-Id`) must be read from inbound
requests (or generated if absent), propagated to all log events for that
request, and echoed back in the response header. This enables end-to-end
request tracing across the API and any downstream services.

## Implementation Steps

### 1. Create Correlation ID Middleware

**File:** `src/HealthPlatform.Api/Middleware/CorrelationIdMiddleware.cs`

```csharp
using Serilog.Context;

namespace HealthPlatform.Api.Middleware;

/// <summary>
/// Reads X-Correlation-Id from the inbound request header, or generates a new
/// GUID if the header is absent. Stores the value in:
/// - HttpContext.Items["CorrelationId"] for downstream middleware/controllers
/// - HttpContext.TraceIdentifier (used by ASP.NET diagnostics)
/// - Serilog LogContext so every log event for this request includes CorrelationId
/// Echoes the value in the X-Correlation-Id response header.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(CorrelationIdHeader, out var incoming)
            ? incoming.ToString()
            : Guid.NewGuid().ToString("D");

        context.Items["CorrelationId"] = correlationId;
        context.TraceIdentifier = correlationId;
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        // Push into Serilog's LogContext so all log events in this request scope carry CorrelationId
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
```

### 2. Register Correlation ID Middleware in `Program.cs`

```csharp
// Correlation ID must be the first custom middleware so all downstream
// log events (including exceptions) carry the correlation ID.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("CorrelationId",
            httpContext.Items.TryGetValue("CorrelationId", out var cid) ? cid : null);
    };
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
});
```

**Pipeline order:**

```
CorrelationIdMiddleware   ← must be first (pushes CorrelationId to LogContext)
UseExceptionHandler       ← catches unhandled exceptions (after correlation ID is set)
UseSerilogRequestLogging  ← logs each request with method/path/status/duration
UseHttpsRedirection
UseAuthorization
MapControllers
MapHealthChecks
```

### 3. Verify `Enrich.FromLogContext()` Is Active

`LogContext.PushProperty` only works when `Enrich.FromLogContext()` is included
in the Serilog configuration (added in Task 001). No further changes needed.

### 4. Output Template Confirmation

The `CompactJsonFormatter` used in Task 001 automatically serialises all
`LogContext` properties as top-level JSON fields. A request log event will look
like:

```json
{
  "@t": "2026-05-26T07:00:00.000Z",
  "@mt": "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms",
  "@r": ["GET", "/api/patients", "200", "12.3456"],
  "RequestMethod": "GET",
  "RequestPath": "/api/patients",
  "StatusCode": 200,
  "Elapsed": 12.3456,
  "CorrelationId": "a3f2b1c4-...",
  "RequestHost": "localhost:5013",
  "RequestScheme": "https",
  "MachineName": "dev-box",
  "EnvironmentName": "Development"
}
```

### 5. Update Serilog Configuration in `appsettings.json` — Request Logging Override

Suppress the noisy default ASP.NET Core request logging (duplicated by
`UseSerilogRequestLogging`):

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Override": {
        "Microsoft.AspNetCore.Hosting.Diagnostics": "Warning",
        "Microsoft.AspNetCore.Routing.EndpointMiddleware": "Warning"
      }
    }
  }
}
```

Add these overrides to the existing `Override` block in both
`appsettings.json` and `appsettings.Development.json`.

## Acceptance Criteria

- [ ] `CorrelationIdMiddleware` exists at `HealthPlatform.Api/Middleware/CorrelationIdMiddleware.cs`
- [ ] Middleware reads `X-Correlation-Id` from request; generates a new `Guid` if absent
- [ ] `X-Correlation-Id` echoed in response headers on every request
- [ ] `CorrelationId` appears as a property in every Serilog log event within the request scope
- [ ] `UseSerilogRequestLogging()` configured with `EnrichDiagnosticContext` for host, scheme, correlationId
- [ ] Every request produces one structured log line with `RequestMethod`, `RequestPath`, `StatusCode`, `Elapsed`
- [ ] `CorrelationIdMiddleware` is registered **before** `UseExceptionHandler` in `Program.cs`
- [ ] `dotnet build` passes with `TreatWarningsAsErrors=true`

## Verification

```bash
# 1. Start API in Development
# 2. Send a request WITHOUT correlation ID header:
curl -v http://localhost:5013/health
# Expected: Response includes "X-Correlation-Id: <generated-guid>" header
# Log line: HTTP GET /health responded 200 in 5.xxxx ms  {CorrelationId: "<same-guid>"}

# 3. Send a request WITH correlation ID header:
curl -v -H "X-Correlation-Id: my-trace-id-123" http://localhost:5013/health
# Expected: Response echoes "X-Correlation-Id: my-trace-id-123"
# Log line: ... {CorrelationId: "my-trace-id-123"}
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| US-006 AC-8 | Request/response logging: method, path, status, duration |
| US-006 AC-9 | Correlation ID via X-Correlation-Id header |
| TR-019 | Serilog structured logging |
| NFR-001 | API response time tracking (Elapsed field) |
