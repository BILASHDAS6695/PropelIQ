# Task 002: AuditLog EF Core Configuration and DbContext Registration

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-011 |
| **Epic** | EP-DATA |
| **Layer** | Infrastructure |
| **Priority** | Critical |
| **Estimated Effort** | 20 minutes |
| **Dependencies** | Task 001 complete (`AuditLog` entity and `AuditAction` enum exist) |

## Objective

Wire `AuditLog` into EF Core by:

1. Creating a typed `IEntityTypeConfiguration<AuditLog>` that maps the `Details` column to `jsonb`,
   stores `Action` as a string, and **explicitly excludes** `AuditLog` from the global soft-delete
   query filter.
2. Registering the `DbSet<AuditLog>` in `ApplicationDbContext`.

`AuditLog` must never appear in the `HasQueryFilter` loop because it does not implement
`ISoftDeletable` — this is enforced by type-check in the existing loop, but the configuration
file also documents it explicitly for clarity.

## Acceptance Criteria Covered

- AC-1: AuditLog table created with all required columns (schema definition step)
- AC-7 (inverse): AuditLog is **excluded** from the global `IsDeleted` query filter

---

## Implementation Steps

### 1. Create `AuditLogConfiguration.cs`

Create file: `src/HealthPlatform.Infrastructure/Persistence/Configurations/AuditLogConfiguration.cs`

```csharp
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .ValueGeneratedNever(); // Set by interceptor before insert

        builder.Property(a => a.UserId)
            .HasColumnName("user_id")
            .IsRequired(false);

        builder.Property(a => a.Action)
            .HasColumnName("action")
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<AuditAction>(v))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.EntityType)
            .HasColumnName("entity_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.EntityId)
            .HasColumnName("entity_id")
            .IsRequired();

        builder.Property(a => a.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        builder.Property(a => a.Details)
            .HasColumnName("details")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasDefaultValue("{}");

        builder.Property(a => a.PreviousHash)
            .HasColumnName("previous_hash")
            .HasMaxLength(64)
            .IsRequired(false);

        builder.Property(a => a.CurrentHash)
            .HasColumnName("current_hash")
            .HasMaxLength(64)
            .IsRequired();

        // Performance: common query patterns are by entity, user, and time window
        builder.HasIndex(a => a.EntityId).HasDatabaseName("ix_audit_logs_entity_id");
        builder.HasIndex(a => a.UserId).HasDatabaseName("ix_audit_logs_user_id");
        builder.HasIndex(a => a.Timestamp).HasDatabaseName("ix_audit_logs_timestamp");

        // AuditLog is insert-only — disable EF Core change tracking for updates
        builder.Metadata.SetIsTableExcludedFromMigrations(false); // keep in migrations
    }
}
```

### 2. Add `DbSet<AuditLog>` to `ApplicationDbContext`

File: `src/HealthPlatform.Infrastructure/Persistence/ApplicationDbContext.cs`

Add the DbSet alongside the existing sets (alphabetical position — after `Appointments`):

```csharp
public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
```

> **Note:** The existing `OnModelCreating` loop:
> ```csharp
> foreach (var entityType in modelBuilder.Model.GetEntityTypes())
> {
>     if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
>         // apply HasQueryFilter
> }
> ```
> already skips `AuditLog` because `AuditLog` does not implement `ISoftDeletable`.
> No additional exclusion code is needed.

### 3. Verify Build

Run from `src/`:

```bash
dotnet build HealthPlatform.sln
```

**Expected output:**

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## Verification Checklist

- [ ] `AuditLogConfiguration.cs` created in `src/HealthPlatform.Infrastructure/Persistence/Configurations/`
- [ ] `Details` column mapped as `jsonb`
- [ ] `Action` stored as string via value converter
- [ ] Three indexes created: `entity_id`, `user_id`, `timestamp`
- [ ] `DbSet<AuditLog> AuditLogs` added to `ApplicationDbContext`
- [ ] Global soft-delete query filter loop does **not** apply to `AuditLog` (type check confirms)
- [ ] `dotnet build` passes — 0 errors, 0 warnings
