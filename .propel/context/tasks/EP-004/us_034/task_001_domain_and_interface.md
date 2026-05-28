# Task 001: Domain Extensions, IInAppNotifier Interface & EF Migration

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-034 |
| **Epic** | EP-004 |
| **Layer** | Domain + Application (interface) + Infrastructure (migration) |
| **Priority** | High |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | US-033 complete — `Notification` entity, `NotificationType`, `NotificationChannel`, `DeliveryStatus` enums already exist |

## Objective

Extend the existing `Notification` entity and related enums to support in-app
notifications for both patients and staff, then define the
`IInAppNotifier` application-layer contract that all event handlers will call.

1. **Extend `Notification` entity** — add `UserId`, `Title`, `Message`,
   `IsRead`, `ReadAt`, `ActionUrl`, and `ExpiresAt` columns.
2. **Make `PatientId` nullable** — staff notifications have no patient context.
3. **Add `InApp` to `NotificationChannel`** enum.
4. **Add `ArrivalAlert`, `StatusChange`, `SwapRequest`, `SwapResult`** to
   `NotificationType` enum (existing values remain unchanged).
5. **Define `IInAppNotifier`** — Application interface for pushing an
   in-app notification (persist + SignalR push).
6. **Apply EF Core migration** to reflect the schema changes.

---

## Acceptance Criteria Covered

- AC: Notifications persisted in database for history (last 90 days)
- AC: Notification types: swap_request, swap_result, appointment_reminder, arrival, status_change
- AC: User offline → delivered on reconnect (notification stored in DB)

---

## Implementation Steps

### 1. Extend `NotificationChannel` enum

File: `src/HealthPlatform.Domain/Enums/NotificationChannel.cs`

```csharp
public enum NotificationChannel
{
    Sms,
    Email,
    InApp,   // ← new
}
```

### 2. Extend `NotificationType` enum

File: `src/HealthPlatform.Domain/Enums/NotificationType.cs`

Add the four new values (existing values keep their integer ordinals — no
breaking change):

```csharp
public enum NotificationType
{
    Reminder,       // 0 — appointment reminder (email + in-app)
    Confirmation,   // 1 — booking/cancellation confirmation (email)
    SlotSwap,       // 2 — legacy; prefer SwapRequest/SwapResult below
    General,        // 3

    // ── In-app notification types (US-034) ───────────────────────────────
    SwapRequest,    // 4 — swap request received (high-priority toast)
    SwapResult,     // 5 — swap request accepted/declined
    ArrivalAlert,   // 6 — patient arrived (high-priority toast for staff)
    StatusChange,   // 7 — appointment status changed
}
```

### 3. Extend `Notification` entity

File: `src/HealthPlatform.Domain/Entities/Notification.cs`

Replace the entire file:

```csharp
using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

public class Notification : BaseEntity
{
    // ── Recipient ────────────────────────────────────────────────────────
    /// <summary>The user who should receive this notification.</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Patient context — null for staff-targeted notifications
    /// (arrival alerts, conflict overrides, etc.).
    /// </summary>
    public Guid? PatientId { get; set; }

    /// <summary>Related appointment — null for non-appointment notifications.</summary>
    public Guid? AppointmentId { get; set; }

    // ── Content ──────────────────────────────────────────────────────────
    public NotificationChannel Channel  { get; set; }
    public NotificationType    Type     { get; set; }
    public string              Title   { get; set; } = string.Empty;
    public string              Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional deep-link route for the Angular SPA, e.g.
    /// <c>/appointments/abc123</c>.
    /// </summary>
    public string? ActionUrl { get; set; }

    // ── Delivery ─────────────────────────────────────────────────────────
    public DeliveryStatus  DeliveryStatus { get; set; }
    public DateTimeOffset  SentAt         { get; set; }

    // ── Read state (in-app only) ─────────────────────────────────────────
    public bool             IsRead  { get; set; }
    public DateTimeOffset?  ReadAt  { get; set; }

    // ── Expiry ───────────────────────────────────────────────────────────
    /// <summary>
    /// UTC expiry for the notification record. Defaults to 90 days after
    /// <see cref="SentAt"/>. Used by the cleanup job.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    // ── Navigation ───────────────────────────────────────────────────────
    public User            User        { get; set; } = null!;
    public PatientProfile? Patient     { get; set; }
    public Appointment?    Appointment { get; set; }
}
```

### 4. Define `IInAppNotifier` interface

Create `src/HealthPlatform.Application/Interfaces/IInAppNotifier.cs`:

```csharp
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Persists an in-app notification to the database and, if the recipient
/// user is connected via SignalR, pushes it in real time.
/// Offline users receive the notification on their next connection
/// (loaded from DB by the Angular client on startup).
/// </summary>
public interface IInAppNotifier
{
    /// <summary>
    /// Persists and optionally pushes a notification to <paramref name="userId"/>.
    /// </summary>
    Task NotifyAsync(
        Guid               userId,
        Guid?              patientId,
        Guid?              appointmentId,
        NotificationType   type,
        string             title,
        string             message,
        string?            actionUrl   = null,
        CancellationToken  ct          = default);
}
```

### 5. Update `User` navigation in `PatientProfile`

`PatientProfile` already has `public User User { get; set; } = null!;` — no
change needed.

Ensure `User` entity has a `Notifications` reverse-navigation collection.

File: `src/HealthPlatform.Domain/Entities/User.cs` — add at the end of the
navigation properties section:

```csharp
public ICollection<Notification> Notifications { get; set; } = [];
```

### 6. Add EF Core migration

```bash
cd src
dotnet ef migrations add AddInAppNotificationColumns \
  --project HealthPlatform.Infrastructure \
  --startup-project HealthPlatform.Api
```

The migration must:
- Add `user_id UUID NOT NULL` (FK → `users.id`)
- Make `patient_id` nullable
- Add `title TEXT NOT NULL DEFAULT ''`
- Add `message TEXT NOT NULL DEFAULT ''`
- Add `action_url TEXT NULL`
- Add `is_read BOOLEAN NOT NULL DEFAULT FALSE`
- Add `read_at TIMESTAMPTZ NULL`
- Add `expires_at TIMESTAMPTZ NOT NULL`
- Update `channel` column to allow new enum value `InApp`

> **If running the tool in an environment without DB access**, create the
> migration scaffold manually and confirm via `dotnet build`.

---

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Domain/Enums/NotificationChannel.cs` | Add `InApp` |
| `src/HealthPlatform.Domain/Enums/NotificationType.cs` | Add `SwapRequest`, `SwapResult`, `ArrivalAlert`, `StatusChange` |
| `src/HealthPlatform.Domain/Entities/Notification.cs` | Add `UserId`, `Title`, `Message`, `ActionUrl`, `IsRead`, `ReadAt`, `ExpiresAt`; make `PatientId` nullable |
| `src/HealthPlatform.Domain/Entities/User.cs` | Add `Notifications` collection |
| `src/HealthPlatform.Application/Interfaces/IInAppNotifier.cs` | New interface |
| `src/HealthPlatform.Infrastructure/Persistence/Migrations/…AddInAppNotificationColumns.cs` | New migration |
| `src/HealthPlatform.Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs` | Updated snapshot |

---

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --no-restore
dotnet test HealthPlatform.Tests/HealthPlatform.Tests.csproj --no-build
```

Expected: build succeeds, all 33 existing tests pass.
