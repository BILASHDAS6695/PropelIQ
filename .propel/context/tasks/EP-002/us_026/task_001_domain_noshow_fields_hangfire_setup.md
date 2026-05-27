# Task 001: Domain No-Show Fields + Hangfire Infrastructure Setup

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-026 |
| **Epic** | EP-002 |
| **Layer** | Domain (entity + migration) + Infrastructure (Hangfire DI) + API (Hangfire dashboard) |
| **Priority** | High |
| **Estimated Effort** | 60 minutes |
| **Dependencies** | None |

## Objective

Three deliverables that form the foundation for all no-show tracking:

1. **Domain** — add `TotalNoShowCount` (int) to `PatientProfile` so the lifetime
   no-show count is always available without a runtime aggregation.  The
   6-month rolling count is computed dynamically from `Appointment` records
   at query time to avoid stale data.  Requires an EF Core migration.

2. **Hangfire setup** — install the three NuGet packages, configure
   `HangfireService` registration in `HealthPlatform.Infrastructure`, wire
   the Hangfire server + dashboard into `Program.cs` (Admin-only).

3. **`HangfireAdminAuthorizationFilter`** — implements
   `IDashboardAuthorizationFilter` so the `/hangfire` dashboard is
   accessible only by authenticated users with the Admin role.

---

## Acceptance Criteria Covered

- AC: Hangfire job runs 30 min after slot end: auto-marks unchecked-in
  appointments as NoShow *(Hangfire infrastructure needed before job is
  registered in Task 003)*
- AC: No-show count tracked on patient profile (lifetime) *(TotalNoShowCount field)*
- AC: Patient with 3+ no-shows in 6 months → visual flag *(count queryable from Appointments)*

---

## Implementation Steps

### 1. Add `TotalNoShowCount` to `PatientProfile` entity

Edit `src/HealthPlatform.Domain/Entities/PatientProfile.cs`.

Add after `InsuranceMemberId`:

```csharp
    public int TotalNoShowCount { get; set; }
```

---

### 2. Configure the new column

Edit `src/HealthPlatform.Infrastructure/Persistence/Configurations/PatientProfileConfiguration.cs`.

Add after the `builder.Property(p => p.InsuranceMemberId)` line:

```csharp
        builder.Property(p => p.TotalNoShowCount).HasDefaultValue(0);
```

---

### 3. Add EF Core migration

Run from repo root:

```powershell
dotnet ef migrations add AddPatientNoShowCount `
    --project  src/HealthPlatform.Infrastructure `
    --startup-project src/HealthPlatform.Api
```

This produces a migration that adds a `total_no_show_count integer NOT NULL DEFAULT 0`
column to the `patient_profiles` table.

---

### 4. Install Hangfire NuGet packages

Run from repo root:

```powershell
# Infrastructure project: core + PostgreSQL storage
dotnet add src/HealthPlatform.Infrastructure/HealthPlatform.Infrastructure.csproj `
    package Hangfire.Core

dotnet add src/HealthPlatform.Infrastructure/HealthPlatform.Infrastructure.csproj `
    package Hangfire.PostgreSql

# API project: ASP.NET Core integration (server + dashboard)
dotnet add src/HealthPlatform.Api/HealthPlatform.Api.csproj `
    package Hangfire.AspNetCore
```

---

### 5. Register Hangfire in `DependencyInjection.cs`

Edit `src/HealthPlatform.Infrastructure/DependencyInjection.cs`.

Add the using directive at the top:

```csharp
using Hangfire;
using Hangfire.PostgreSql;
```

Add after the `services.AddScoped<IEmailSender, NoOpEmailSender>();` line and
before `services.Configure<AccountSecuritySettings>(...)`:

```csharp
        services.AddHangfire(config =>
            config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(c =>
                    c.UseNpgsqlConnection(
                        configuration.GetConnectionString("DefaultConnection")!)));

        services.AddHangfireServer();
```

---

### 6. Create `HangfireAdminAuthorizationFilter`

Create a new file:
`src/HealthPlatform.Api/Authorization/HangfireAdminAuthorizationFilter.cs`

```csharp
using Hangfire.Dashboard;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Api.Authorization;

/// <summary>
/// Restricts the Hangfire dashboard to authenticated users with the Admin role.
/// Hangfire's <see cref="IDashboardAuthorizationFilter"/> runs outside of
/// ASP.NET Core's normal authorization pipeline, so we inspect the
/// <see cref="HttpContext"/> directly.
/// </summary>
public sealed class HangfireAdminAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true
            && httpContext.User.IsInRole(nameof(UserRole.Admin));
    }
}
```

---

### 7. Wire Hangfire into `Program.cs`

Edit `src/HealthPlatform.Api/Program.cs`.

Add the using directive at the top if not already present:

```csharp
using Hangfire;
using HealthPlatform.Api.Authorization;
```

After `app.UseAuthorization();` and before the controller/hub map calls, add:

```csharp
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireAdminAuthorizationFilter()]
});
```

---

## Files Modified / Created

| Path | Action |
|------|--------|
| `src/HealthPlatform.Domain/Entities/PatientProfile.cs` | Edit — add `TotalNoShowCount` |
| `src/HealthPlatform.Infrastructure/Persistence/Configurations/PatientProfileConfiguration.cs` | Edit — configure default 0 |
| `src/HealthPlatform.Infrastructure/Migrations/XXXXXXXX_AddPatientNoShowCount.cs` | Auto-generated by EF |
| `src/HealthPlatform.Infrastructure/DependencyInjection.cs` | Edit — register Hangfire |
| `src/HealthPlatform.Api/Authorization/HangfireAdminAuthorizationFilter.cs` | Create |
| `src/HealthPlatform.Api/Program.cs` | Edit — `UseHangfireDashboard` |

## Verification

- [ ] `dotnet build src/HealthPlatform.sln` passes with no errors
- [ ] Migration script contains `total_no_show_count integer not null default 0`
- [ ] `dotnet ef database update ...` applies cleanly against the dev database
- [ ] `GET /hangfire` returns 401 for unauthenticated requests
- [ ] `GET /hangfire` returns 403 for authenticated non-Admin users
- [ ] `GET /hangfire` loads the dashboard for an Admin user
