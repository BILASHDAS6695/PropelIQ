# Task 002: Core Domain Entity Classes

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-008 |
| **Epic** | EP-DATA |
| **Layer** | Domain |
| **Priority** | Critical |
| **Estimated Effort** | 60 minutes |
| **Dependencies** | None (parallel to Task 001) |

## Objective

Create all 15 core domain entity classes in `HealthPlatform.Domain/Entities/` and
all supporting enumerations in `HealthPlatform.Domain/Enums/` so that:

1. Every entity from the ERD (model.md §4) has a matching C# class.
2. All entities that require audit columns extend `AuditableEntity`; identity-only
   entities extend `BaseEntity`.
3. All `DateTime` fields use `DateTimeOffset` for timezone-safe storage.
4. JSON columns (DataJson, ConsolidatedDataJson, Details) are typed as `JsonDocument`
   to enable future querying via EF Core's `ToJsonb()`.

## Acceptance Criteria Covered

- AC-2: `ApplicationDbContext` created with entity sets for all core entities
  (entity classes are the prerequisite — DbSets are added in Task 003)
- AC-6: Base entity configurations: `Id` (UUID), `CreatedAt`, `UpdatedAt`
  auto-populated (satisfied by inheriting `AuditableEntity`)

---

## Implementation Steps

### 1. Create Enum Files in `HealthPlatform.Domain/Enums/`

Create the directory and one file per logical group:

**`UserRole.cs`**
```csharp
namespace HealthPlatform.Domain.Enums;

public enum UserRole
{
    Patient,
    Staff,
    Admin
}
```

**`AppointmentStatus.cs`**
```csharp
namespace HealthPlatform.Domain.Enums;

public enum AppointmentStatus
{
    Booked,
    Arrived,
    Completed,
    Cancelled,
    NoShow
}
```

**`PreferredSlotStatus.cs`**
```csharp
namespace HealthPlatform.Domain.Enums;

public enum PreferredSlotStatus
{
    Pending,
    Swapped,
    Expired,
    Cancelled
}
```

**`IntakeMode.cs`**
```csharp
namespace HealthPlatform.Domain.Enums;

public enum IntakeMode
{
    AiConversational,
    ManualForm
}
```

**`DocumentProcessingStatus.cs`**
```csharp
namespace HealthPlatform.Domain.Enums;

public enum DocumentProcessingStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
```

**`DataCategory.cs`**
```csharp
namespace HealthPlatform.Domain.Enums;

public enum DataCategory
{
    Medication,
    Diagnosis,
    Vital,
    Procedure,
    Allergy
}
```

**`DataConflictSeverity.cs`**
```csharp
namespace HealthPlatform.Domain.Enums;

public enum DataConflictSeverity
{
    Critical,
    Warning,
    Info
}
```

**`ResolutionStatus.cs`**
```csharp
namespace HealthPlatform.Domain.Enums;

public enum ResolutionStatus
{
    Unresolved,
    Resolved,
    Dismissed
}
```

**`MedicalCodeType.cs`**
```csharp
namespace HealthPlatform.Domain.Enums;

public enum MedicalCodeType
{
    Icd10,
    Cpt
}
```

**`NotificationChannel.cs`**
```csharp
namespace HealthPlatform.Domain.Enums;

public enum NotificationChannel
{
    Sms,
    Email
}
```

**`NotificationType.cs`**
```csharp
namespace HealthPlatform.Domain.Enums;

public enum NotificationType
{
    Reminder,
    Confirmation,
    SlotSwap,
    General
}
```

**`DeliveryStatus.cs`**
```csharp
namespace HealthPlatform.Domain.Enums;

public enum DeliveryStatus
{
    Pending,
    Sent,
    Delivered,
    Failed
}
```

**`InsuranceStatus.cs`**
```csharp
namespace HealthPlatform.Domain.Enums;

public enum InsuranceStatus
{
    Active,
    Inactive
}
```

---

### 2. Create Entity Files in `HealthPlatform.Domain/Entities/`

**`User.cs`**
```csharp
using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

public class User : AuditableEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    public PatientProfile? PatientProfile { get; set; }
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
}
```

**`PatientProfile.cs`**
```csharp
using HealthPlatform.Domain.Common;

namespace HealthPlatform.Domain.Entities;

public class PatientProfile : AuditableEntity
{
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly Dob { get; set; }
    public string? Phone { get; set; }
    public string? InsuranceProviderName { get; set; }
    public string? InsuranceMemberId { get; set; }

    public User User { get; set; } = null!;
    public ICollection<Appointment> Appointments { get; set; } = [];
    public ICollection<ClinicalDocument> ClinicalDocuments { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
    public PatientView360? PatientView360 { get; set; }
}
```

**`Provider.cs`**
```csharp
using HealthPlatform.Domain.Common;

namespace HealthPlatform.Domain.Entities;

public class Provider : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Specialty { get; set; }
    public Guid? ScheduleTemplateId { get; set; }

    public ICollection<AppointmentSlot> AppointmentSlots { get; set; } = [];
    public ICollection<Appointment> Appointments { get; set; } = [];
}
```

**`AppointmentSlot.cs`**
```csharp
using HealthPlatform.Domain.Common;

namespace HealthPlatform.Domain.Entities;

public class AppointmentSlot : BaseEntity
{
    public Guid ProviderId { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public bool IsAvailable { get; set; }

    public Provider Provider { get; set; } = null!;
    public Appointment? Appointment { get; set; }
}
```

**`Appointment.cs`**
```csharp
using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

public class Appointment : AuditableEntity
{
    public Guid PatientId { get; set; }
    public Guid ProviderId { get; set; }
    public Guid SlotId { get; set; }
    public DateTimeOffset SlotTime { get; set; }
    public AppointmentStatus Status { get; set; }
    public Guid? PreferredSlotId { get; set; }
    public bool IsWalkIn { get; set; }

    public PatientProfile Patient { get; set; } = null!;
    public Provider Provider { get; set; } = null!;
    public AppointmentSlot Slot { get; set; } = null!;
    public IntakeRecord? IntakeRecord { get; set; }
    public PreferredSlotPreference? PreferredSlotPreference { get; set; }
    public ICollection<Notification> Notifications { get; set; } = [];
}
```

**`PreferredSlotPreference.cs`**
```csharp
using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

public class PreferredSlotPreference : BaseEntity
{
    public Guid AppointmentId { get; set; }
    public Guid PreferredSlotId { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
    public PreferredSlotStatus Status { get; set; }

    public Appointment Appointment { get; set; } = null!;
}
```

**`IntakeRecord.cs`**
```csharp
using System.Text.Json;
using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

public class IntakeRecord : AuditableEntity
{
    public Guid PatientId { get; set; }
    public Guid AppointmentId { get; set; }
    public IntakeMode Mode { get; set; }
    public JsonDocument? DataJson { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public PatientProfile Patient { get; set; } = null!;
    public Appointment Appointment { get; set; } = null!;
}
```

**`ClinicalDocument.cs`**
```csharp
using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

public class ClinicalDocument : AuditableEntity
{
    public Guid PatientId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public DocumentProcessingStatus ProcessingStatus { get; set; }

    public PatientProfile Patient { get; set; } = null!;
    public ICollection<ExtractedData> ExtractedData { get; set; } = [];
}
```

**`ExtractedData.cs`**
```csharp
using System.Text.Json;
using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

public class ExtractedData : BaseEntity
{
    public Guid DocumentId { get; set; }
    public Guid PatientId { get; set; }
    public DataCategory DataCategory { get; set; }
    public JsonDocument? DataJson { get; set; }
    public int ConfidenceScore { get; set; }
    public int PageNumber { get; set; }

    public ClinicalDocument Document { get; set; } = null!;
}
```

**`PatientView360.cs`**
```csharp
using System.Text.Json;
using HealthPlatform.Domain.Common;

namespace HealthPlatform.Domain.Entities;

public class PatientView360 : BaseEntity
{
    public Guid PatientId { get; set; }
    public JsonDocument? ConsolidatedDataJson { get; set; }
    public DateTimeOffset LastUpdatedAt { get; set; }
    public int ConflictCount { get; set; }

    public PatientProfile Patient { get; set; } = null!;
    public ICollection<DataConflict> DataConflicts { get; set; } = [];
    public ICollection<MedicalCode> MedicalCodes { get; set; } = [];
}
```

**`DataConflict.cs`**
```csharp
using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

public class DataConflict : BaseEntity
{
    public Guid PatientViewId { get; set; }
    public string Field { get; set; } = string.Empty;
    public string ValueA { get; set; } = string.Empty;
    public string ValueB { get; set; } = string.Empty;
    public Guid SourceDocA { get; set; }
    public Guid SourceDocB { get; set; }
    public DataConflictSeverity Severity { get; set; }
    public ResolutionStatus ResolutionStatus { get; set; }
    public Guid? ResolvedBy { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    public PatientView360 PatientView { get; set; } = null!;
}
```

**`MedicalCode.cs`**
```csharp
using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

public class MedicalCode : BaseEntity
{
    public Guid PatientViewId { get; set; }
    public MedicalCodeType CodeType { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Confidence { get; set; }
    public Guid? VerifiedBy { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }

    public PatientView360 PatientView { get; set; } = null!;
}
```

**`AuditLog.cs`**
```csharp
using System.Text.Json;
using HealthPlatform.Domain.Common;

namespace HealthPlatform.Domain.Entities;

public class AuditLog : BaseEntity
{
    public Guid UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public JsonDocument? Details { get; set; }
    public string? PreviousHash { get; set; }
    public string CurrentHash { get; set; } = string.Empty;

    public User User { get; set; } = null!;
}
```

**`Notification.cs`**
```csharp
using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

public class Notification : BaseEntity
{
    public Guid PatientId { get; set; }
    public Guid? AppointmentId { get; set; }
    public NotificationChannel Channel { get; set; }
    public NotificationType Type { get; set; }
    public DateTimeOffset SentAt { get; set; }
    public DeliveryStatus DeliveryStatus { get; set; }

    public PatientProfile Patient { get; set; } = null!;
    public Appointment? Appointment { get; set; }
}
```

**`InsuranceRecord.cs`**
```csharp
using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

public class InsuranceRecord : BaseEntity
{
    public string ProviderName { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public InsuranceStatus Status { get; set; }
}
```

---

## Notes

- **`JsonDocument`** requires `System.Text.Json`. No additional NuGet package is needed.
- **`DateOnly`** is supported by Npgsql ≥ 6 and maps to PostgreSQL `date` type natively.
- Enum values are stored as `integer` by default in EF Core — the entity type
  configurations (Task 003) will convert them to `string` via `HasConversion<string>()`
  for readability in the database.
- Navigation properties use C# 12 primary collection initialiser syntax (`[]`) to avoid
  null reference warnings without requiring lazy loading.

## Verification

```bash
cd src
dotnet build HealthPlatform.sln
```

Expected: zero errors. New namespace `HealthPlatform.Domain.Entities` appears in the
build output. IntelliSense should resolve all entity types across projects.
