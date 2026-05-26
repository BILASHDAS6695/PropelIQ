# Task 001: EF Core DbContext Conventions — Snake_case, UTC, and Connection Pooling

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-008 |
| **Epic** | EP-DATA |
| **Layer** | Infrastructure |
| **Priority** | Critical |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | None |

## Objective

Configure `ApplicationDbContext` and `DependencyInjection.cs` so that:

1. All table/column names follow PostgreSQL snake_case convention (`UseSnakeCaseNamingConvention()`).
2. Npgsql connection pool is capped at **100 connections** (`MaxPoolSize=100`).
3. Every `DateTime` property retrieved from PostgreSQL is guaranteed to carry `DateTimeKind.Utc`
   via a global value-converter convention registered in `OnModelCreating`.

These three settings are global — applying them now means every entity added later in Task 002/003
automatically inherits the correct naming, timezone, and pooling behaviour.

## Acceptance Criteria Covered

- AC-4: Npgsql connection pooling configured (max 100 connections)
- AC-5: EF Core conventions applied: UTC datetime conversion, snake_case naming

---

## Implementation Steps

### 1. Add `EFCore.NamingConventions` NuGet Package

`UseSnakeCaseNamingConvention()` is shipped by the separate `EFCore.NamingConventions` package,
not by `Npgsql.EntityFrameworkCore.PostgreSQL`. Add it to
`src/HealthPlatform.Infrastructure/HealthPlatform.Infrastructure.csproj`:

```xml
<PackageReference Include="EFCore.NamingConventions" Version="8.0.3" />
```

Run from `src/`:

```bash
dotnet add HealthPlatform.Infrastructure/HealthPlatform.Infrastructure.csproj package EFCore.NamingConventions --version 8.0.3
```

### 2. Update `DependencyInjection.cs` — Snake_case + Connection Pooling

File: `src/HealthPlatform.Infrastructure/DependencyInjection.cs`

Replace the existing `AddDbContext` registration:

```csharp
// BEFORE
services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions => npgsqlOptions.MigrationsAssembly(
            typeof(ApplicationDbContext).Assembly.FullName)));
```

With the updated version that adds snake_case and `MaxPoolSize`:

```csharp
// AFTER
services.AddDbContext<ApplicationDbContext>(options =>
    options
        .UseNpgsql(
            configuration.GetConnectionString("DefaultConnection"),
            npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                npgsqlOptions.MaxBatchSize(100);
            })
        .UseSnakeCaseNamingConvention());
```

> **Connection pooling note**: Npgsql manages its own connection pool outside of EF Core.
> Pool size is controlled via the connection string parameter `Maximum Pool Size`.
> Add it to `appsettings.json` (Step 3) rather than through an EF Core option.

### 3. Add `Maximum Pool Size` to Connection String in `appsettings.json`

File: `src/HealthPlatform.Api/appsettings.json`

Update the `DefaultConnection` value to include the pool ceiling:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=healthplatform;Username=postgres;Password=postgres;Maximum Pool Size=100",
  "Redis": "localhost:6379"
}
```

The `docker-compose.yml` already overrides `ConnectionStrings__DefaultConnection` via environment
variable — add the pool parameter there too:

```yaml
ConnectionStrings__DefaultConnection: >-
  Host=postgres;Port=5432;Database=${POSTGRES_DB};
  Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD};
  Maximum Pool Size=100
```

### 4. Add UTC DateTime Value-Converter Convention to `ApplicationDbContext`

File: `src/HealthPlatform.Infrastructure/Persistence/ApplicationDbContext.cs`

Add the following `using` directives:

```csharp
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
```

Update `OnModelCreating` to apply the UTC convention **after**
`ApplyConfigurationsFromAssembly`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

    // Ensure all DateTime values round-trip as UTC
    var utcConverter = new ValueConverter<DateTime, DateTime>(
        toDb:   v => v.ToUniversalTime(),
        fromDb: v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    var utcNullableConverter = new ValueConverter<DateTime?, DateTime?>(
        toDb:   v => v.HasValue ? v.Value.ToUniversalTime() : v,
        fromDb: v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        foreach (var property in entityType.GetProperties())
        {
            if (property.ClrType == typeof(DateTime))
                property.SetValueConverter(utcConverter);
            else if (property.ClrType == typeof(DateTime?))
                property.SetValueConverter(utcNullableConverter);
        }
    }

    base.OnModelCreating(modelBuilder);
}
```

> `DateTimeOffset` already encodes the UTC offset and does not need a converter.
> Only bare `DateTime` columns get the convention.

---

## Verification

Run the following after changes are applied:

```bash
cd src
dotnet build HealthPlatform.sln
```

Expected: build succeeds with no errors or new warnings.

No database connection is needed at this stage — the conventions are applied at
model-building time and will be visible in the migration generated in Task 004.
