# Task 004: Infrastructure Layer — EF Core & Npgsql Configuration

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-001 |
| **Epic** | EP-TECH |
| **Layer** | Infrastructure |
| **Priority** | Critical |
| **Estimated Effort** | 2 hours |
| **Dependencies** | Task 001, Task 002 |

## Objective

Configure the Infrastructure layer with Entity Framework Core and Npgsql (PostgreSQL) packages, establish the DbContext with auditable entity interceptor support, and provide DI registration.

## Implementation Steps

### 1. Add NuGet Packages

```bash
cd HealthPlatform.Infrastructure
dotnet add package Microsoft.EntityFrameworkCore --version 8.*
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.*
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.*
dotnet add package Microsoft.Extensions.Configuration.Abstractions
```

### 2. Create Application DbContext

**File:** `HealthPlatform.Infrastructure/Persistence/ApplicationDbContext.cs`

```csharp
using HealthPlatform.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HealthPlatform.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditableEntities();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateAuditableEntities()
    {
        var entries = ChangeTracker.Entries<AuditableEntity>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTimeOffset.UtcNow;
                entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
    }
}
```

### 3. Create DI Registration Extension

**File:** `HealthPlatform.Infrastructure/DependencyInjection.cs`

```csharp
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

        return services;
    }
}
```

### 4. Remove Default Class1.cs

Delete auto-generated `Class1.cs`.

## Acceptance Criteria

- [ ] `Microsoft.EntityFrameworkCore` 8.x package installed
- [ ] `Npgsql.EntityFrameworkCore.PostgreSQL` 8.x package installed
- [ ] `ApplicationDbContext` extends `DbContext`
- [ ] `SaveChangesAsync` override populates `CreatedAt`/`UpdatedAt` on auditable entities
- [ ] `DependencyInjection.AddInfrastructure()` registers DbContext with Npgsql provider
- [ ] Connection string sourced from `IConfiguration`
- [ ] Infrastructure project builds with zero warnings

## Verification

```bash
dotnet build HealthPlatform.Infrastructure/HealthPlatform.Infrastructure.csproj
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| TR-008 | .NET 8 EF Core |
| US-001 AC-5 | EF Core and Npgsql packages |
| ADR-001 | Single database (Modular Monolith) |
