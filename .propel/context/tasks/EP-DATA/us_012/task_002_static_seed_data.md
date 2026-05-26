# Task 002: Static Seed Data — Providers and Insurance Records

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-012 |
| **Epic** | EP-DATA |
| **Layer** | Infrastructure (EF Core configurations + migration) |
| **Priority** | High |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | Task 001 (build must pass); US-008 (Npgsql + migrations pipeline) |

## Objective

Seed 5 providers with distinct medical specialties and 50 insurance records
(name + member ID combinations) into the database using EF Core's `HasData`
API so that the data is baked into the migration and present on every fresh
`database update` — no manual SQL scripts required.

## Acceptance Criteria Covered

- AC-1: Seed data creates 5 providers with different specialties
- AC-2: Seed data creates 50+ dummy insurance records (name + member ID combinations)
- AC-8: Seed data runs automatically on first migration apply (via `HasData`)

## Implementation Steps

### 1. Update `ProviderConfiguration.cs` — Add `HasData`

Providers inherit `AuditableEntity`, so `CreatedAt`, `UpdatedAt`, and `IsDeleted`
must be supplied with fixed values. Use a sentinel timestamp of
`2025-01-01T00:00:00Z` so the migration is deterministic.

Modify `src/HealthPlatform.Infrastructure/Persistence/Configurations/ProviderConfiguration.cs`:

```csharp
using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class ProviderConfiguration : IEntityTypeConfiguration<Provider>
{
    private static readonly DateTimeOffset SeedDate =
        new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<Provider> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Specialty).HasMaxLength(100);

        builder.HasData(
            new Provider
            {
                Id          = Guid.Parse("11111111-0000-0000-0000-000000000001"),
                Name        = "Dr. Sarah Mitchell",
                Specialty   = "Cardiology",
                CreatedAt   = SeedDate,
                UpdatedAt   = SeedDate,
                IsDeleted   = false
            },
            new Provider
            {
                Id          = Guid.Parse("11111111-0000-0000-0000-000000000002"),
                Name        = "Dr. James Okafor",
                Specialty   = "General Practice",
                CreatedAt   = SeedDate,
                UpdatedAt   = SeedDate,
                IsDeleted   = false
            },
            new Provider
            {
                Id          = Guid.Parse("11111111-0000-0000-0000-000000000003"),
                Name        = "Dr. Priya Sharma",
                Specialty   = "Neurology",
                CreatedAt   = SeedDate,
                UpdatedAt   = SeedDate,
                IsDeleted   = false
            },
            new Provider
            {
                Id          = Guid.Parse("11111111-0000-0000-0000-000000000004"),
                Name        = "Dr. Marcus Chen",
                Specialty   = "Orthopedics",
                CreatedAt   = SeedDate,
                UpdatedAt   = SeedDate,
                IsDeleted   = false
            },
            new Provider
            {
                Id          = Guid.Parse("11111111-0000-0000-0000-000000000005"),
                Name        = "Dr. Fatima Al-Rashid",
                Specialty   = "Pediatrics",
                CreatedAt   = SeedDate,
                UpdatedAt   = SeedDate,
                IsDeleted   = false
            }
        );
    }
}
```

### 2. Update `InsuranceRecordConfiguration.cs` — Add `HasData`

`InsuranceRecord` inherits `BaseEntity` (only `Id`), so seed rows are simpler.
Generate 50 records across 10 fictional insurance carriers × 5 member IDs each.

Modify `src/HealthPlatform.Infrastructure/Persistence/Configurations/InsuranceRecordConfiguration.cs`:

```csharp
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class InsuranceRecordConfiguration : IEntityTypeConfiguration<InsuranceRecord>
{
    public void Configure(EntityTypeBuilder<InsuranceRecord> builder)
    {
        builder.HasKey(ir => ir.Id);

        builder.Property(ir => ir.ProviderName).IsRequired().HasMaxLength(200);
        builder.Property(ir => ir.MemberId).IsRequired().HasMaxLength(100);
        builder.Property(ir => ir.Status)
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.HasData(GenerateSeedRecords());
    }

    private static InsuranceRecord[] GenerateSeedRecords()
    {
        var carriers = new[]
        {
            "BlueCross BlueShield", "Aetna Health", "United Healthcare",
            "Cigna Medical", "Humana Insurance", "Anthem BCBS",
            "Molina Healthcare", "Centene Corporation", "WellCare Health",
            "Kaiser Permanente"
        };

        var records = new List<InsuranceRecord>();
        var baseIndex = 1;

        foreach (var carrier in carriers)
        {
            for (var i = 1; i <= 5; i++)
            {
                records.Add(new InsuranceRecord
                {
                    Id           = Guid.Parse($"22222222-0000-0000-0000-{baseIndex:D12}"),
                    ProviderName = carrier,
                    MemberId     = $"MBR-{baseIndex:D6}",
                    Status       = baseIndex % 7 == 0 ? InsuranceStatus.Inactive
                                                      : InsuranceStatus.Active
                });
                baseIndex++;
            }
        }

        return [.. records];
    }
}
```

### 3. Generate EF Core Migration

```bash
cd src
dotnet ef migrations add AddSeedData \
    --project HealthPlatform.Infrastructure \
    --startup-project HealthPlatform.Api \
    --output-dir Persistence/Migrations
```

### 4. Verify Migration Content

After generation, confirm the migration file contains:
- `migrationBuilder.InsertData(table: "providers", ...)` — 5 rows
- `migrationBuilder.InsertData(table: "insurance_records", ...)` — 50 rows

The `Down()` method must contain the corresponding `DeleteData` calls.

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Infrastructure/Persistence/Configurations/ProviderConfiguration.cs` | Add `HasData` for 5 providers |
| `src/HealthPlatform.Infrastructure/Persistence/Configurations/InsuranceRecordConfiguration.cs` | Add `HasData` for 50 insurance records |
| `src/HealthPlatform.Infrastructure/Persistence/Migrations/YYYYMMDDHHMMSS_AddSeedData.cs` | New EF migration (auto-generated) |
| `src/HealthPlatform.Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs` | Updated by EF tooling |

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Release
dotnet test HealthPlatform.sln --no-build --configuration Release
```

Confirm the new migration file exists and contains `InsertData` for both
tables before committing.

## Notes

- Fixed `Guid` IDs in `HasData` are essential — EF uses them as primary keys
  in migration `InsertData`/`DeleteData` pairs. Changing them after the first
  `database update` is a breaking change requiring a manual data fix.
- The sentinel `SeedDate` (2025-01-01 UTC) satisfies the non-nullable
  `CreatedAt`/`UpdatedAt` columns; the audit interceptor in `ApplicationDbContext`
  only fires on `ChangeTracker` entries, not on `HasData` rows.
- `InsuranceStatus.Inactive` is assigned to every 7th record (indices 7, 14, 21 …)
  to provide realistic mixed-status data for feature development.
- The `$"22222222-0000-0000-0000-{baseIndex:D12}"` pattern produces valid GUIDs
  (`000000000001` through `000000000050`) that are clearly identifiable as seed
  data in logs and SQL queries.
- Do NOT apply the migration to production yet — appointment slots (Task 003)
  use a hosted service, not `HasData`, but both tasks should be committed
  together before the first `database update`.
