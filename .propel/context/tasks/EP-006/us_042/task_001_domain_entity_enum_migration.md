# Task 001: Domain Entity Update, IntakeStatus Enum & EF Migration

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-042 |
| **Epic** | EP-006 |
| **Layer** | .NET — Domain, Infrastructure, EF Core migration |
| **Priority** | High |
| **Estimated Effort** | 20 minutes |
| **Dependencies** | US-041 complete — `IntakeRecord` entity and `IntakeRecordConfiguration` exist |

## Objective

1. **Add `IntakeStatus` enum** — `Draft`, `Completed`, `ReviewedByProvider`, `Orphaned`
2. **Add `IntakeData` record** — typed CSHARP record representing the JSONB schema (chiefComplaint, symptoms, medications, allergies, medicalHistory, severity, duration)
3. **Update `IntakeRecord` entity** — add `Status`, `ReviewedAt`, `ReviewedByProviderId`, replace raw `JsonDocument?` with typed `IntakeData?`
4. **Update `IntakeRecordConfiguration`** — configure new columns; keep JSONB serialization
5. **Add EF migration** — `AddIntakeStatusAndReviewFields`

---

## Acceptance Criteria Covered

- AC: Status tracking: Draft, Completed, ReviewedByProvider
- AC: Provider can mark intake as "Reviewed" (timestamp + providerId)
- AC: Schema: chiefComplaint, symptoms[], medications[], allergies[], medicalHistory, severity, duration
- AC: IntakeRecord linked to: patientId, appointmentId, completedAt timestamp

---

## Implementation Steps

### 1. Create `IntakeStatus` enum

Create `src/HealthPlatform.Domain/Enums/IntakeStatus.cs`:

```csharp
namespace HealthPlatform.Domain.Enums;

public enum IntakeStatus
{
    Draft,
    Completed,
    ReviewedByProvider,
    Orphaned,
}
```

### 2. Create `IntakeData` record

Create `src/HealthPlatform.Domain/ValueObjects/IntakeData.cs`:

```csharp
namespace HealthPlatform.Domain.ValueObjects;

/// <summary>
/// Typed representation of the JSONB intake payload stored in IntakeRecord.DataJson.
/// </summary>
public sealed record IntakeData
{
    public string ChiefComplaint { get; init; } = string.Empty;
    public List<string> Symptoms { get; init; } = [];
    public string Duration { get; init; } = string.Empty;
    public int Severity { get; init; } = 5;          // 1–10
    public List<string> Medications { get; init; } = [];
    public List<string> Allergies { get; init; } = [];
    public string MedicalHistory { get; init; } = string.Empty;
}
```

### 3. Update `IntakeRecord` entity

Replace `src/HealthPlatform.Domain/Entities/IntakeRecord.cs`:

```csharp
using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;
using HealthPlatform.Domain.ValueObjects;

namespace HealthPlatform.Domain.Entities;

public class IntakeRecord : AuditableEntity
{
    public Guid PatientId { get; set; }
    public Guid AppointmentId { get; set; }
    public IntakeMode Mode { get; set; }
    public IntakeStatus Status { get; set; } = IntakeStatus.Draft;
    public IntakeData? Data { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedByProviderId { get; set; }

    // Navigation
    public PatientProfile Patient { get; set; } = null!;
    public Appointment Appointment { get; set; } = null!;
}
```

### 4. Update `IntakeRecordConfiguration`

Replace `src/HealthPlatform.Infrastructure/Persistence/Configurations/IntakeRecordConfiguration.cs`:

```csharp
using System.Text.Json;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class IntakeRecordConfiguration : IEntityTypeConfiguration<IntakeRecord>
{
    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<IntakeRecord> builder)
    {
        builder.HasKey(ir => ir.Id);

        builder.Property(ir => ir.Mode)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(ir => ir.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(ir => ir.Data)
            .HasColumnType("jsonb")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, JsonOpts),
                v => v == null ? null : JsonSerializer.Deserialize<IntakeData>(v, JsonOpts))
            .HasColumnName("data_json");

        builder.HasOne(ir => ir.Appointment)
            .WithOne(a => a.IntakeRecord)
            .HasForeignKey<IntakeRecord>(ir => ir.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

### 5. Add EF Migration

Run:

```bash
cd src
dotnet ef migrations add AddIntakeStatusAndReviewFields \
  --project HealthPlatform.Infrastructure \
  --startup-project HealthPlatform.Api
```

Then verify the generated migration file adds:
- `status` column (varchar 30, not null, default `'Draft'`)
- `data_json` column (jsonb, nullable) — renames from `DataJson`
- `reviewed_at` column (timestamptz, nullable)
- `reviewed_by_provider_id` column (uuid, nullable)
- Drops old `data_json` if column name changed

---

## Verification

```bash
cd src
dotnet build
dotnet test
```

Expected: build clean, 58/58 tests green.
