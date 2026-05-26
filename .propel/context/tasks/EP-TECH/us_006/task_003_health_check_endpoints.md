# Task 003: Health Check Endpoints

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-006 |
| **Epic** | EP-TECH |
| **Layer** | API / Infrastructure |
| **Priority** | High |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | Task 001 (Serilog setup for health check logging) |

## Objective

Expose a `/health` endpoint that reports liveness and component readiness
(PostgreSQL, Redis). The response is machine-readable JSON so orchestrators
(Docker health checks, Kubernetes probes, load balancers) can act on it.
Health check registrations live in `HealthPlatform.Infrastructure` so each
layer owns its own dependency checks.

## Implementation Steps

### 1. Add NuGet Packages

**`src/HealthPlatform.Api/HealthPlatform.Api.csproj`:**
```xml
<PackageReference Include="AspNetCore.HealthChecks.UI.Client" Version="8.0.1" />
```

**`src/HealthPlatform.Infrastructure/HealthPlatform.Infrastructure.csproj`:**
```xml
<PackageReference Include="AspNetCore.HealthChecks.NpgSql" Version="8.0.1" />
<PackageReference Include="AspNetCore.HealthChecks.Redis" Version="8.0.1" />
<PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" Version="8.0.11" />
```

### 2. Register Health Checks in `HealthPlatform.Infrastructure/DependencyInjection.cs`

Add a `AddInfrastructureHealthChecks` extension (or extend `AddInfrastructure`)
that registers DB and Redis checks:

```csharp
using AspNetCore.HealthChecks.UI.Client;
using HealthPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HealthPlatform.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsqlOptions => npgsqlOptions.MigrationsAssembly(
                    typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddHealthChecks()
            .AddNpgSql(
                configuration.GetConnectionString("DefaultConnection")!,
                name: "postgres",
                tags: ["db", "ready"])
            .AddDbContextCheck<ApplicationDbContext>(
                name: "efcore",
                tags: ["db", "ready"])
            .AddRedis(
                configuration.GetConnectionString("Redis") ?? "localhost:6379",
                name: "redis",
                tags: ["cache", "ready"]);

        return services;
    }
}
```

### 3. Add Redis Connection String to `appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=healthplatform;Username=postgres;Password=postgres",
    "Redis": "localhost:6379"
  }
}
```

### 4. Map the `/health` Endpoint in `Program.cs`

Add the health check middleware and endpoint mapping. Use `UIResponseWriter`
for machine-readable JSON output including component statuses:

```csharp
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

// After app.UseAuthorization():
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

### 5. Health Check Response Format

`UIResponseWriter` produces a JSON body like:

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0123456",
  "entries": {
    "postgres": { "status": "Healthy", "duration": "00:00:00.005" },
    "efcore":   { "status": "Healthy", "duration": "00:00:00.003" },
    "redis":    { "status": "Healthy", "duration": "00:00:00.002" }
  }
}
```

HTTP status codes:
- `200 OK` — all checks `Healthy`
- `503 Service Unavailable` — any check `Degraded` or `Unhealthy`

### 6. Update `docker-compose.yml` Health Check (optional, low-priority)

The API service in `docker-compose.yml` may already reference `/health`. If it
uses a different path, update it:

```yaml
healthcheck:
  test: ["CMD", "curl", "-f", "http://localhost:5013/health"]
  interval: 30s
  timeout: 10s
  retries: 3
  start_period: 40s
```

## Acceptance Criteria

- [ ] `AspNetCore.HealthChecks.NpgSql` and `AspNetCore.HealthChecks.Redis` added to `Infrastructure.csproj`
- [ ] `AspNetCore.HealthChecks.UI.Client` added to `Api.csproj`
- [ ] Health checks registered in `AddInfrastructure()`: `postgres`, `efcore`, `redis`
- [ ] `"Redis"` connection string key present in `appsettings.json`
- [ ] `/health` endpoint mapped using `UIResponseWriter.WriteHealthCheckUIResponse`
- [ ] `GET /health` returns `200` JSON with component entries when all dependencies are up
- [ ] `GET /health` returns `503` when any dependency is down
- [ ] `dotnet build` passes with `TreatWarningsAsErrors=true`

## Verification

```bash
# Start docker-compose stack, then:
curl -s http://localhost:5013/health | jq .
# Expected: {"status":"Healthy","entries":{"postgres":{"status":"Healthy",...},...}}

# To verify 503 on failure, stop postgres temporarily:
docker-compose stop db
curl -o /dev/null -w "%{http_code}" http://localhost:5013/health
# Expected: 503
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| US-006 AC-5 | Health check endpoint at /health (DB + Redis) |
| TR-021 | ASP.NET Health Checks |
| NFR-001 | API response time tracking (health entry durations) |
