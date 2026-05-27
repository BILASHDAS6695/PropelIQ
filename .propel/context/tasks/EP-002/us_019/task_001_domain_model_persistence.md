# Task 001: Domain Model + Persistence Layer

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-019 |
| **Epic** | EP-002 |
| **Layer** | Domain + Infrastructure (EF config + migration) |
| **Priority** | Critical |
| **Estimated Effort** | 60 minutes |
| **Dependencies** | None — foundation task |

## Objective

Extend the domain model to support provider schedule rules and unavailability
blocks, and upgrade `AppointmentSlot` with a tri-state `SlotStatus` enum.
Wire all changes through EF Core configurations and produce a single migration.

## Acceptance Criteria Covered

- AC: Admin can define recurring weekly schedule per provider (day + start/end time)
- AC: Default slot duration: 30 minutes (configurable per provider)
- AC: Admin can mark specific dates as unavailable (vacation, holidays)
- AC: Each slot has status (Available/Booked/Blocked)

---

## Implementation Steps

### 1. Add `SlotStatus` Enum

Create `src/HealthPlatform.Domain/Enums/SlotStatus.cs`:

```csharp
namespace HealthPlatform.Domain.Enums;

public enum SlotStatus
{
    Available = 0,
    Booked    = 1,
    Blocked   = 2
}
```

---

### 2. Add `ProviderScheduleRule` Entity

Create `src/HealthPlatform.Domain/Entities/ProviderScheduleRule.cs`:

```csharp
using HealthPlatform.Domain.Common;

namespace HealthPlatform.Domain.Entities;

/// <summary>
/// Defines a recurring weekly availability window for a provider.
/// Slot generation reads these rules to produce <see cref="AppointmentSlot"/>
/// records for the next 90 days.
/// </summary>
public class ProviderScheduleRule : AuditableEntity
{
    public Guid       ProviderId           { get; set; }
    public DayOfWeek  DayOfWeek            { get; set; }
    public TimeOnly   StartTime            { get; set; }
    public TimeOnly   EndTime              { get; set; }
    public int        SlotDurationMinutes  { get; set; } = 30;

    public Provider Provider { get; set; } = null!;
}
```

---

### 3. Add `ProviderUnavailability` Entity

Create `src/HealthPlatform.Domain/Entities/ProviderUnavailability.cs`:

```csharp
using HealthPlatform.Domain.Common;

namespace HealthPlatform.Domain.Entities;

/// <summary>
/// Marks a specific calendar date as unavailable for a provider
/// (vacation, public holiday, personal leave, etc.).
/// Slot generation skips these dates when producing <see cref="AppointmentSlot"/>
/// records.
/// </summary>
public class ProviderUnavailability : AuditableEntity
{
    public Guid     ProviderId       { get; set; }
    public DateOnly UnavailableDate  { get; set; }
    public string?  Reason           { get; set; }

    public Provider Provider { get; set; } = null!;
}
```

---

### 4. Update `AppointmentSlot` — Replace `IsAvailable` with `SlotStatus`

Edit `src/HealthPlatform.Domain/Entities/AppointmentSlot.cs`:

```csharp
using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

public class AppointmentSlot : BaseEntity
{
    public Guid       ProviderId { get; set; }
    public DateTimeOffset StartTime  { get; set; }
    public DateTimeOffset EndTime    { get; set; }
    public SlotStatus Status     { get; set; } = SlotStatus.Available;

    public Provider      Provider    { get; set; } = null!;
    public Appointment?  Appointment { get; set; }
}
```

> **Note**: `IsAvailable` is removed. All usages in the seed service and
> specifications must be updated in Tasks 002/003.

---

### 5. Update `Provider` — Add Navigation Properties

Edit `src/HealthPlatform.Domain/Entities/Provider.cs` — add the two new collections:

```csharp
using HealthPlatform.Domain.Common;

namespace HealthPlatform.Domain.Entities;

public class Provider : AuditableEntity
{
    public string  Name               { get; set; } = string.Empty;
    public string? Specialty          { get; set; }
    public Guid?   ScheduleTemplateId { get; set; }

    public ICollection<AppointmentSlot>       AppointmentSlots  { get; set; } = [];
    public ICollection<Appointment>           Appointments      { get; set; } = [];
    public ICollection<ProviderScheduleRule>  ScheduleRules     { get; set; } = [];
    public ICollection<ProviderUnavailability> Unavailabilities { get; set; } = [];
}
```

---

### 6. Update `AppointmentSlotConfiguration`

Edit `src/HealthPlatform.Infrastructure/Persistence/Configurations/AppointmentSlotConfiguration.cs`:

```csharp
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class AppointmentSlotConfiguration : IEntityTypeConfiguration<AppointmentSlot>
{
    public void Configure(EntityTypeBuilder<AppointmentSlot> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(SlotStatus.Available);

        builder.HasIndex(s => new { s.ProviderId, s.StartTime });
        builder.HasIndex(s => s.Status);

        builder.HasOne(s => s.Provider)
            .WithMany(p => p.AppointmentSlots)
            .HasForeignKey(s => s.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

---

### 7. Add `ProviderScheduleRuleConfiguration`

Create `src/HealthPlatform.Infrastructure/Persistence/Configurations/ProviderScheduleRuleConfiguration.cs`:

```csharp
using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class ProviderScheduleRuleConfiguration
    : IEntityTypeConfiguration<ProviderScheduleRule>
{
    public void Configure(EntityTypeBuilder<ProviderScheduleRule> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.DayOfWeek)
            .HasConversion<int>();

        builder.Property(r => r.SlotDurationMinutes)
            .HasDefaultValue(30);

        // Unique: one rule per (provider, day-of-week). Overlapping
        // day-of-week rules for the same provider are rejected at creation time.
        builder.HasIndex(r => new { r.ProviderId, r.DayOfWeek })
            .IsUnique();

        builder.HasOne(r => r.Provider)
            .WithMany(p => p.ScheduleRules)
            .HasForeignKey(r => r.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

---

### 8. Add `ProviderUnavailabilityConfiguration`

Create `src/HealthPlatform.Infrastructure/Persistence/Configurations/ProviderUnavailabilityConfiguration.cs`:

```csharp
using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class ProviderUnavailabilityConfiguration
    : IEntityTypeConfiguration<ProviderUnavailability>
{
    public void Configure(EntityTypeBuilder<ProviderUnavailability> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Reason).HasMaxLength(500);

        // Unique: one unavailability record per (provider, date).
        builder.HasIndex(u => new { u.ProviderId, u.UnavailableDate })
            .IsUnique();

        builder.HasOne(u => u.Provider)
            .WithMany(p => p.Unavailabilities)
            .HasForeignKey(u => u.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

---

### 9. Generate EF Migration

```bash
cd src
dotnet ef migrations add AddProviderScheduleEntities \
    --project HealthPlatform.Infrastructure \
    --startup-project HealthPlatform.Api \
    --output-dir Persistence/Migrations
```

The migration will:
- Create `provider_schedule_rules` table
- Create `provider_unavailabilities` table
- Update `appointment_slots`: drop `is_available` column, add `status` column
  with default `'Available'`

> **Migration data note**: Existing `is_available = true` rows will default to
> `'Available'` via the column default. `is_available = false` rows (Blocked
> slots) require a manual data migration in `Up()`:
> ```csharp
> migrationBuilder.Sql(
>     "UPDATE appointment_slots SET status = 'Blocked' WHERE is_available = false;");
> ```
> Add this SQL *before* dropping the `is_available` column.

---

### 10. Register New DbSets in `ApplicationDbContext`

Edit `src/HealthPlatform.Infrastructure/Persistence/ApplicationDbContext.cs` to
add:

```csharp
public DbSet<ProviderScheduleRule>   ProviderScheduleRules   => Set<ProviderScheduleRule>();
public DbSet<ProviderUnavailability> ProviderUnavailabilities => Set<ProviderUnavailability>();
```

---

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Domain/Enums/SlotStatus.cs` | New |
| `src/HealthPlatform.Domain/Entities/ProviderScheduleRule.cs` | New |
| `src/HealthPlatform.Domain/Entities/ProviderUnavailability.cs` | New |
| `src/HealthPlatform.Domain/Entities/AppointmentSlot.cs` | Replace `IsAvailable` with `Status` |
| `src/HealthPlatform.Domain/Entities/Provider.cs` | Add `ScheduleRules` + `Unavailabilities` nav props |
| `src/HealthPlatform.Infrastructure/Persistence/ApplicationDbContext.cs` | Add 2 new DbSets |
| `src/HealthPlatform.Infrastructure/Persistence/Configurations/AppointmentSlotConfiguration.cs` | Map `Status`, add index |
| `src/HealthPlatform.Infrastructure/Persistence/Configurations/ProviderScheduleRuleConfiguration.cs` | New |
| `src/HealthPlatform.Infrastructure/Persistence/Configurations/ProviderUnavailabilityConfiguration.cs` | New |
| `src/HealthPlatform.Infrastructure/Persistence/Migrations/YYYYMMDDHHMMSS_AddProviderScheduleEntities.cs` | New |

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Release
dotnet test HealthPlatform.sln --no-build --configuration Release
```

No compile errors. Tests still pass (no Application/API code changed yet).
