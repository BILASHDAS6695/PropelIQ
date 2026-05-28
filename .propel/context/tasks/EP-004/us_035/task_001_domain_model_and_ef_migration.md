# Task 001: NotificationPreferences Domain Model, UserConfiguration Update & EF Migration

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-035 |
| **Epic** | EP-004 |
| **Layer** | Domain + Application (interface) + Infrastructure (config + migration) |
| **Priority** | Low |
| **Estimated Effort** | 25 minutes |
| **Dependencies** | US-034 complete — `Notification` entity, `IInAppNotifier`, `SignalRInAppNotifier`, `NotificationsController` all in place |

## Objective

1. **Create `NotificationPreferences`** value object in the Domain layer — a
   simple POCO with six boolean flags (email/in-app × reminders/swap/general)
   defaulting to `true`.
2. **Add `NotificationPreferences` property to `User`** entity.
3. **Update `UserConfiguration`** to persist the value object as a `jsonb`
   column via JSON serialization.
4. **Define `INotificationPreferenceChecker`** Application interface — checked
   by notification senders before delivery to respect user opt-outs.
5. **Apply EF Core migration** to add the `notification_preferences` column to
   the `users` table with a default of `'{}'` (interpreted as all-true by the
   deserializer).

---

## Acceptance Criteria Covered

- AC: Preferences stored in user profile (JSONB column)
- AC: Default preferences: all channels enabled for all categories
- AC: Changes take effect immediately (no restart needed)

---

## Implementation Steps

### 1. Create `NotificationPreferences` value object

Create `src/HealthPlatform.Domain/ValueObjects/NotificationPreferences.cs`:

```csharp
namespace HealthPlatform.Domain.ValueObjects;

/// <summary>
/// Stores per-channel, per-category notification opt-in flags for a user.
/// All flags default to <c>true</c> (opt-in) when not explicitly set.
/// Serialised as a JSONB column on the <c>users</c> table.
/// </summary>
public sealed class NotificationPreferences
{
    // ── Email channel ─────────────────────────────────────────────────────
    public bool EmailReminders { get; set; } = true;
    public bool EmailSwap      { get; set; } = true;
    public bool EmailGeneral   { get; set; } = true;

    // ── In-app channel ────────────────────────────────────────────────────
    public bool InAppReminders { get; set; } = true;
    public bool InAppSwap      { get; set; } = true;
    public bool InAppGeneral   { get; set; } = true;
}
```

**Category mapping** (used by `NotificationPreferenceCheckerService` in Task 002):

| `NotificationType`            | Category   |
|-------------------------------|------------|
| `Reminder`                    | Reminders  |
| `SwapRequest`, `SwapResult`, `SlotSwap` | Swap |
| `Confirmation`, `General`, `ArrivalAlert`, `StatusChange` | General |

---

### 2. Add property to `User` entity

File: `src/HealthPlatform.Domain/Entities/User.cs`

Add the property at the end of the class body (after `Notifications`):

```csharp
    public NotificationPreferences NotificationPreferences { get; set; } = new();
```

Add the using at the top of the file:

```csharp
using HealthPlatform.Domain.ValueObjects;
```

---

### 3. Update `UserConfiguration`

File: `src/HealthPlatform.Infrastructure/Persistence/Configurations/UserConfiguration.cs`

Add at the end of the `Configure` method body (before the closing brace), after
the existing `builder.HasMany(u => u.AuditLogs)` block:

```csharp
        // Notification preferences stored as a JSONB blob.
        // Deserialization defaults to all-enabled when the column is '{}' or null.
        builder.Property(u => u.NotificationPreferences)
            .HasColumnName("notification_preferences")
            .HasColumnType("jsonb")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v,
                    (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<
                    HealthPlatform.Domain.ValueObjects.NotificationPreferences>(
                    v,
                    (System.Text.Json.JsonSerializerOptions?)null)
                    ?? new HealthPlatform.Domain.ValueObjects.NotificationPreferences());
```

---

### 4. Define `INotificationPreferenceChecker` interface

Create `src/HealthPlatform.Application/Interfaces/INotificationPreferenceChecker.cs`:

```csharp
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Checks whether a given notification channel + type combination is
/// permitted for the specified user based on their stored preferences.
/// </summary>
/// <remarks>
/// Security notifications (account lockout, credential expiry) are sent
/// directly through <see cref="IEmailSender"/> without a
/// <see cref="HealthPlatform.Domain.Enums.NotificationType"/> and therefore
/// bypass this check by design — they are always delivered.
/// </remarks>
public interface INotificationPreferenceChecker
{
    /// <summary>
    /// Returns <c>true</c> when the user has the channel + type combination
    /// enabled (or when no preference record exists — default-open).
    /// </summary>
    Task<bool> IsAllowedAsync(
        Guid                userId,
        NotificationChannel channel,
        NotificationType    type,
        CancellationToken   ct = default);
}
```

---

### 5. Apply EF Core migration

Run the following from the solution root (`src/`):

```bash
cd src
dotnet ef migrations add AddNotificationPreferencesToUser \
    --project HealthPlatform.Infrastructure/HealthPlatform.Infrastructure.csproj \
    --startup-project HealthPlatform.Api/HealthPlatform.Api.csproj \
    --output-dir Persistence/Migrations
dotnet ef database update \
    --project HealthPlatform.Infrastructure/HealthPlatform.Infrastructure.csproj \
    --startup-project HealthPlatform.Api/HealthPlatform.Api.csproj
```

> **Expected migration content** — the generated `Up()` method should add a
> nullable `jsonb` column `notification_preferences` to the `users` table.
> If the scaffolded migration contains additional unexpected changes, inspect
> the `ModelSnapshot` diff before applying.

---

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Release
# Expect: Build succeeded. 0 Error(s)

dotnet test HealthPlatform.Tests/HealthPlatform.Tests.csproj --no-build
# Expect: Passed! — 33/33 (no new tests in this task — existing tests must still pass)
```

---

## Notes

- `NotificationPreferences` is `sealed` and has no domain behaviour — it is a
  pure data bag serialised to JSON. A Domain value object with `Equals`/`GetHashCode`
  override is not needed given EF Core handles it through JSON conversion.
- The `jsonb` type stores the JSON as binary in PostgreSQL, allowing future
  indexed queries on individual flags if needed.
- Keeping the default `= new()` on the entity property means in-memory objects
  always have non-null preferences even before the first save.
- The `INotificationPreferenceChecker` interface lives in Application so that
  Application-layer handlers (e.g., `InitiateSwapRequestCommandHandler`) can
  depend on it without referencing Infrastructure.
