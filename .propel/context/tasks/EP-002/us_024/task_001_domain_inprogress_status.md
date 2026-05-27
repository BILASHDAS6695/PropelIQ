# Task 001: Domain — Add `InProgress` Status

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-024 |
| **Epic** | EP-002 |
| **Layer** | Domain |
| **Priority** | High |
| **Estimated Effort** | 10 minutes |
| **Dependencies** | None |

## Objective

Add `InProgress = 7` to `AppointmentStatus` to represent an appointment where
the provider has begun the consultation.  This is the new step in the
`Arrived → InProgress → Completed` provider-driven status chain.

`AppointmentStatus` is stored as a string column (`HasConversion<string>()`,
`HasMaxLength(20)`).  "InProgress" is 10 characters — well within the existing
limit.  No ALTER TABLE and no EF migration are required.

## Acceptance Criteria Covered

- AC: Provider can change appointment status: Arrived → InProgress → Completed
- AC: Color coding: InProgress (green)

---

## Implementation Steps

### 1. Add `InProgress` to `AppointmentStatus`

Edit `src/HealthPlatform.Domain/Enums/AppointmentStatus.cs`.

Add `InProgress = 7` after `WalkIn`:

```csharp
namespace HealthPlatform.Domain.Enums;

public enum AppointmentStatus
{
    Scheduled  = 0,   // Initial state: booked online, not yet checked in
    Booked     = 1,   // Confirmed / checked in at clinic
    Arrived    = 2,
    Completed  = 3,
    Cancelled  = 4,
    NoShow     = 5,
    WalkIn     = 6,   // Unscheduled walk-in; uses QueuePosition instead of SlotId
    InProgress = 7    // Provider has started the consultation
}
```

---

## Verification

```bash
dotnet build src/HealthPlatform.sln
# Expected: 0 errors, 0 warnings
```

No migration file needed. The DB column is `varchar(20)` with no check constraint
— the new string value is persisted transparently.
