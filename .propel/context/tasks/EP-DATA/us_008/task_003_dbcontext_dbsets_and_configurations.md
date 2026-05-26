# Task 003: ApplicationDbContext DbSets and Entity Type Configurations

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-008 |
| **Epic** | EP-DATA |
| **Layer** | Infrastructure |
| **Priority** | Critical |
| **Estimated Effort** | 60 minutes |
| **Dependencies** | Task 001 (conventions), Task 002 (entity classes) |

## Objective

Register all 15 core entities with EF Core so that:

1. `ApplicationDbContext` exposes a typed `DbSet<T>` for each entity.
2. Each entity has a corresponding `IEntityTypeConfiguration<T>` class that explicitly
   sets: primary key behaviour, unique indexes, foreign-key delete rules, enum-to-string
   conversions, and JSONB column types.
3. No table or column names are defined in the configuration files — all naming comes from
   the `UseSnakeCaseNamingConvention()` applied in Task 001.

## Acceptance Criteria Covered

- AC-2: `ApplicationDbContext` created with entity sets for all core entities
- AC-5: EF Core conventions applied (complemented by explicit FK and index config)
- AC-6: Base entity `Id` (UUID) is set as the primary key; `CreatedAt`/`UpdatedAt`
  are populated via the `SaveChangesAsync` override already in place.

---

## Implementation Steps

### 1. Add DbSet Properties to `ApplicationDbContext`

File: `src/HealthPlatform.Infrastructure/Persistence/ApplicationDbContext.cs`

Add `using` for the Entities namespace and `DbSet<T>` properties:

```csharp
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HealthPlatform.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // ── Entity Sets ──────────────────────────────────────────────────────────
    public DbSet<User> Users => Set<User>();
    public DbSet<PatientProfile> PatientProfiles => Set<PatientProfile>();
    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<AppointmentSlot> AppointmentSlots => Set<AppointmentSlot>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<PreferredSlotPreference> PreferredSlotPreferences => Set<PreferredSlotPreference>();
    public DbSet<IntakeRecord> IntakeRecords => Set<IntakeRecord>();
    public DbSet<ClinicalDocument> ClinicalDocuments => Set<ClinicalDocument>();
    public DbSet<ExtractedData> ExtractedData => Set<ExtractedData>();
    public DbSet<PatientView360> PatientViews360 => Set<PatientView360>();
    public DbSet<DataConflict> DataConflicts => Set<DataConflict>();
    public DbSet<MedicalCode> MedicalCodes => Set<MedicalCode>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<InsuranceRecord> InsuranceRecords => Set<InsuranceRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Global UTC DateTime value-converter convention (from Task 001)
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

> Use `Set<T>()` expression-body properties rather than auto-properties with setters.
> This pattern avoids EF Core warnings about `DbSet<T>` properties not being
> initialised by the constructor.

---

### 2. Create Entity Type Configuration Files

Create the directory `src/HealthPlatform.Infrastructure/Persistence/Configurations/`
and add one file per entity.

#### `UserConfiguration.cs`

```csharp
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasMany(u => u.AuditLogs)
            .WithOne(al => al.User)
            .HasForeignKey(al => al.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

#### `PatientProfileConfiguration.cs`

```csharp
using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class PatientProfileConfiguration : IEntityTypeConfiguration<PatientProfile>
{
    public void Configure(EntityTypeBuilder<PatientProfile> builder)
    {
        builder.HasKey(p => p.Id);

        builder.HasIndex(p => p.UserId).IsUnique();

        builder.Property(p => p.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(p => p.LastName).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Phone).HasMaxLength(20);
        builder.Property(p => p.InsuranceProviderName).HasMaxLength(200);
        builder.Property(p => p.InsuranceMemberId).HasMaxLength(100);

        builder.HasOne(p => p.User)
            .WithOne(u => u.PatientProfile)
            .HasForeignKey<PatientProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

#### `ProviderConfiguration.cs`

```csharp
using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class ProviderConfiguration : IEntityTypeConfiguration<Provider>
{
    public void Configure(EntityTypeBuilder<Provider> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Specialty).HasMaxLength(100);
    }
}
```

#### `AppointmentSlotConfiguration.cs`

```csharp
using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class AppointmentSlotConfiguration : IEntityTypeConfiguration<AppointmentSlot>
{
    public void Configure(EntityTypeBuilder<AppointmentSlot> builder)
    {
        builder.HasKey(s => s.Id);

        builder.HasIndex(s => new { s.ProviderId, s.StartTime });

        builder.HasOne(s => s.Provider)
            .WithMany(p => p.AppointmentSlots)
            .HasForeignKey(s => s.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

#### `AppointmentConfiguration.cs`

```csharp
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(a => a.PatientId);
        builder.HasIndex(a => a.ProviderId);
        builder.HasIndex(a => a.SlotId).IsUnique();

        builder.HasOne(a => a.Patient)
            .WithMany(p => p.Appointments)
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Provider)
            .WithMany(p => p.Appointments)
            .HasForeignKey(a => a.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Slot)
            .WithOne(s => s.Appointment)
            .HasForeignKey<Appointment>(a => a.SlotId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

#### `PreferredSlotPreferenceConfiguration.cs`

```csharp
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class PreferredSlotPreferenceConfiguration
    : IEntityTypeConfiguration<PreferredSlotPreference>
{
    public void Configure(EntityTypeBuilder<PreferredSlotPreference> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(p => p.Appointment)
            .WithOne(a => a.PreferredSlotPreference)
            .HasForeignKey<PreferredSlotPreference>(p => p.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

#### `IntakeRecordConfiguration.cs`

```csharp
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class IntakeRecordConfiguration : IEntityTypeConfiguration<IntakeRecord>
{
    public void Configure(EntityTypeBuilder<IntakeRecord> builder)
    {
        builder.HasKey(ir => ir.Id);

        builder.Property(ir => ir.Mode)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(ir => ir.DataJson)
            .HasColumnType("jsonb");

        builder.HasOne(ir => ir.Appointment)
            .WithOne(a => a.IntakeRecord)
            .HasForeignKey<IntakeRecord>(ir => ir.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

#### `ClinicalDocumentConfiguration.cs`

```csharp
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class ClinicalDocumentConfiguration : IEntityTypeConfiguration<ClinicalDocument>
{
    public void Configure(EntityTypeBuilder<ClinicalDocument> builder)
    {
        builder.HasKey(cd => cd.Id);

        builder.Property(cd => cd.FileName).IsRequired().HasMaxLength(500);
        builder.Property(cd => cd.StoragePath).IsRequired().HasMaxLength(1000);

        builder.Property(cd => cd.ProcessingStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(cd => cd.PatientId);

        builder.HasOne(cd => cd.Patient)
            .WithMany(p => p.ClinicalDocuments)
            .HasForeignKey(cd => cd.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

#### `ExtractedDataConfiguration.cs`

```csharp
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class ExtractedDataConfiguration : IEntityTypeConfiguration<ExtractedData>
{
    public void Configure(EntityTypeBuilder<ExtractedData> builder)
    {
        builder.HasKey(ed => ed.Id);

        builder.Property(ed => ed.DataCategory)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(ed => ed.DataJson)
            .HasColumnType("jsonb");

        builder.HasIndex(ed => ed.DocumentId);
        builder.HasIndex(ed => ed.PatientId);

        builder.HasOne(ed => ed.Document)
            .WithMany(cd => cd.ExtractedData)
            .HasForeignKey(ed => ed.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

#### `PatientView360Configuration.cs`

```csharp
using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class PatientView360Configuration : IEntityTypeConfiguration<PatientView360>
{
    public void Configure(EntityTypeBuilder<PatientView360> builder)
    {
        builder.HasKey(pv => pv.Id);

        builder.HasIndex(pv => pv.PatientId).IsUnique();

        builder.Property(pv => pv.ConsolidatedDataJson)
            .HasColumnType("jsonb");

        builder.HasOne(pv => pv.Patient)
            .WithOne(p => p.PatientView360)
            .HasForeignKey<PatientView360>(pv => pv.PatientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

#### `DataConflictConfiguration.cs`

```csharp
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class DataConflictConfiguration : IEntityTypeConfiguration<DataConflict>
{
    public void Configure(EntityTypeBuilder<DataConflict> builder)
    {
        builder.HasKey(dc => dc.Id);

        builder.Property(dc => dc.Field).IsRequired().HasMaxLength(200);
        builder.Property(dc => dc.ValueA).IsRequired().HasMaxLength(1000);
        builder.Property(dc => dc.ValueB).IsRequired().HasMaxLength(1000);

        builder.Property(dc => dc.Severity)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(dc => dc.ResolutionStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(dc => dc.PatientViewId);

        builder.HasOne(dc => dc.PatientView)
            .WithMany(pv => pv.DataConflicts)
            .HasForeignKey(dc => dc.PatientViewId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

#### `MedicalCodeConfiguration.cs`

```csharp
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class MedicalCodeConfiguration : IEntityTypeConfiguration<MedicalCode>
{
    public void Configure(EntityTypeBuilder<MedicalCode> builder)
    {
        builder.HasKey(mc => mc.Id);

        builder.Property(mc => mc.CodeType)
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(mc => mc.Code).IsRequired().HasMaxLength(20);
        builder.Property(mc => mc.Description).IsRequired().HasMaxLength(500);

        builder.HasIndex(mc => mc.PatientViewId);

        builder.HasOne(mc => mc.PatientView)
            .WithMany(pv => pv.MedicalCodes)
            .HasForeignKey(mc => mc.PatientViewId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

#### `AuditLogConfiguration.cs`

```csharp
using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(al => al.Id);

        builder.Property(al => al.Action).IsRequired().HasMaxLength(200);
        builder.Property(al => al.EntityType).IsRequired().HasMaxLength(200);
        builder.Property(al => al.CurrentHash).IsRequired().HasMaxLength(64);
        builder.Property(al => al.PreviousHash).HasMaxLength(64);

        builder.Property(al => al.Details)
            .HasColumnType("jsonb");

        // Audit logs are immutable — no cascaded updates allowed
        builder.HasIndex(al => al.UserId);
        builder.HasIndex(al => new { al.EntityType, al.EntityId });

        builder.HasOne(al => al.User)
            .WithMany(u => u.AuditLogs)
            .HasForeignKey(al => al.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

#### `NotificationConfiguration.cs`

```csharp
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Channel)
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(n => n.Type)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(n => n.DeliveryStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(n => n.PatientId);

        builder.HasOne(n => n.Patient)
            .WithMany(p => p.Notifications)
            .HasForeignKey(n => n.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.Appointment)
            .WithMany(a => a.Notifications)
            .HasForeignKey(n => n.AppointmentId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
```

#### `InsuranceRecordConfiguration.cs`

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
    }
}
```

---

## Notes

- All configuration classes are `internal sealed` — they are implementation details of
  the Infrastructure layer and must not leak into other assemblies.
- `DeleteBehavior.Restrict` is used for cross-aggregate foreign keys (e.g., Notification →
  Patient) to enforce explicit deletion logic in the application layer.
- `DeleteBehavior.Cascade` is used for owned/dependent entities (e.g., ExtractedData →
  ClinicalDocument) where the child has no meaning without the parent.
- `HasColumnType("jsonb")` on `JsonDocument` columns enables PostgreSQL's JSONB operator
  queries (`@>`, `?`, etc.) in future features.

## Verification

```bash
cd src
dotnet build HealthPlatform.sln
```

Expected: zero errors. Confirm `ApplicationDbContext` exposes 15 `DbSet<T>` properties.
All configuration classes are picked up automatically via
`ApplyConfigurationsFromAssembly()` already registered in `OnModelCreating`.
