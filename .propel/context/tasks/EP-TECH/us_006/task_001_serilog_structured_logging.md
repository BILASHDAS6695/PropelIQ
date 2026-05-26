# Task 001: Serilog Structured Logging Setup

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-006 |
| **Epic** | EP-TECH |
| **Layer** | API / Infrastructure (cross-cutting) |
| **Priority** | Critical |
| **Estimated Effort** | 1 hour |
| **Dependencies** | None (first task) |

## Objective

Replace the default Microsoft.Extensions.Logging host with Serilog, outputting
JSON-structured logs enriched with environment metadata and per-request
correlation IDs. Log levels must be fully configurable via `appsettings.json`
without recompilation.

## Implementation Steps

### 1. Add NuGet Packages to `HealthPlatform.Api.csproj`

```xml
<PackageReference Include="Serilog.AspNetCore" Version="8.0.3" />
<PackageReference Include="Serilog.Enrichers.Environment" Version="3.0.1" />
<PackageReference Include="Serilog.Enrichers.Thread" Version="4.0.0" />
<PackageReference Include="Serilog.Expressions" Version="5.0.0" />
```

`Serilog.AspNetCore` transitively includes `Serilog`, `Serilog.Sinks.Console`,
and `Serilog.Extensions.Hosting`. `Serilog.Expressions` enables structured
output templates and JSON formatting without a separate JSON sink package.

### 2. Bootstrap Serilog in `Program.cs`

Replace the existing host builder with Serilog bootstrap logging and
`UseSerilog()` configuration-driven setup:

```csharp
using HealthPlatform.Application;
using HealthPlatform.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration)
                     .ReadFrom.Services(services)
                     .Enrich.FromLogContext()
                     .Enrich.WithMachineName()
                     .Enrich.WithEnvironmentName()
                     .Enrich.WithThreadId());

    // Layer registrations
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // API services
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
```

The `HostAbortedException` exclusion prevents Serilog from logging normal EF
Core design-time host-creation aborts as fatal errors.

### 3. Configure Serilog in `appsettings.json`

Replace the existing `Logging` section with a `Serilog` section:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=healthplatform;Username=postgres;Password=postgres"
  },
  "Serilog": {
    "Using": ["Serilog.Sinks.Console"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.Hosting.Lifetime": "Information",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithEnvironmentName", "WithThreadId"],
    "Properties": {
      "Application": "HealthPlatform.Api"
    }
  },
  "AllowedHosts": "*"
}
```

### 4. Add `Serilog.Formatting.Compact` package

```xml
<PackageReference Include="Serilog.Formatting.Compact" Version="3.0.0" />
```

`CompactJsonFormatter` produces compact, single-line JSON log events suitable
for log aggregators (Seq, Elasticsearch, Loki).

### 5. Update `appsettings.Development.json`

Override minimum level to `Debug` in development so all diagnostic messages
surface during local development:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft": "Information",
        "Microsoft.Hosting.Lifetime": "Information",
        "System": "Information"
      }
    }
  }
}
```

## Acceptance Criteria

- [ ] `Serilog.AspNetCore` and enricher packages added to `HealthPlatform.Api.csproj`
- [ ] `Program.cs` uses two-stage initialisation: bootstrap logger → `UseSerilog()` from config
- [ ] Application starts without errors; startup log lines are JSON on console
- [ ] Log level is `Information` in production profile, `Debug` in Development
- [ ] `MachineName`, `EnvironmentName`, `ThreadId` appear as properties in every log event
- [ ] Fatal exception during startup is logged before process exit with full stack trace

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Release
dotnet run --project HealthPlatform.Api -- --environment Development
# Console output should be compact JSON lines, e.g.:
# {"@t":"...","@mt":"Application started","MachineName":"...","EnvironmentName":"Development",...}
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| US-006 AC-1 | Serilog JSON structured output |
| US-006 AC-2 | Log levels configurable via appsettings |
| TR-019 | Serilog structured logging |
