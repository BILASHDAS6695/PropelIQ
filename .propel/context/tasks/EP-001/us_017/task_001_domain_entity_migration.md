# Task 001: Domain Entity Fields, EF Configuration, Migration & Settings

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-017 |
| **Epic** | EP-001 |
| **Layer** | Domain · Infrastructure · Configuration |
| **Priority** | High |
| **Estimated Effort** | 1.5 hours |
| **Dependencies** | None — foundational task |

## Objective

Add the four account-security fields to the `User` entity, configure them in EF Core, generate the database migration, register `AccountSecuritySettings` as a typed-options class, and expose default values in `appsettings.json`.

---

## Implementation Steps

### 1. `src/HealthPlatform.Domain/Entities/User.cs` — Add 4 new properties

Add the following after `LastLoginAt`:

```csharp
// ── Account lockout ───────────────────────────────────────────────────
/// <summary>Number of consecutive failed login attempts since last success.</summary>
public int FailedLoginAttempts { get; set; }

/// <summary>
/// UTC timestamp at which the account lockout expires.
/// <c>null</c> means the account is not currently locked.
/// </summary>
public DateTimeOffset? LockoutEnd { get; set; }

// ── Password policy ───────────────────────────────────────────────────
/// <summary>
/// UTC timestamp when the current password expires and must be changed.
/// Set to <c>UtcNow + 90 days</c> on every successful password change.
/// </summary>
public DateTimeOffset? CredentialExpiresAt { get; set; }

/// <summary>
/// Ordered list of the last N bcrypt hashes (most recent first).
/// Populated and trimmed by <c>ChangePasswordCommandHandler</c>.
/// Stored as a JSON array in the database column <c>password_history</c>.
/// </summary>
public List<string> PasswordHistory { get; set; } = [];
```

### 2. `src/HealthPlatform.Infrastructure/Persistence/Configurations/UserConfiguration.cs` — Map new columns

Inside `Configure(EntityTypeBuilder<User> builder)`:

```csharp
builder.Property(u => u.FailedLoginAttempts)
    .HasColumnName("failed_login_attempts")
    .HasDefaultValue(0);

builder.Property(u => u.LockoutEnd)
    .HasColumnName("lockout_end")
    .HasColumnType("timestamp with time zone");

builder.Property(u => u.CredentialExpiresAt)
    .HasColumnName("credential_expires_at")
    .HasColumnType("timestamp with time zone");

builder.Property(u => u.PasswordHistory)
    .HasColumnName("password_history")
    .HasColumnType("jsonb")
    .HasConversion(
        v => System.Text.Json.JsonSerializer.Serialize(v,
            (System.Text.Json.JsonSerializerOptions?)null),
        v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v,
            (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>())
    .HasDefaultValueSql("'[]'::jsonb");
```

### 3. `src/HealthPlatform.Application/Settings/AccountSecuritySettings.cs` — Create typed-options class

**File path:** `src/HealthPlatform.Application/Settings/AccountSecuritySettings.cs`

```csharp
namespace HealthPlatform.Application.Settings;

/// <summary>
/// Strongly-typed options for account lockout and password-rotation policy.
/// Bound from the <c>AccountSecurity</c> section of <c>appsettings.json</c>.
/// </summary>
public sealed class AccountSecuritySettings
{
    public const string SectionName = "AccountSecurity";

    /// <summary>Max consecutive failed logins before lockout. Default: 5.</summary>
    public int MaxFailedLoginAttempts { get; set; } = 5;

    /// <summary>Duration of lockout in minutes. Default: 15.</summary>
    public int LockoutDurationMinutes { get; set; } = 15;

    /// <summary>Days until the current password must be changed. Default: 90.</summary>
    public int PasswordExpiryDays { get; set; } = 90;

    /// <summary>Number of previous hashes to store and reject reuse against. Default: 5.</summary>
    public int PasswordHistorySize { get; set; } = 5;
}
```

### 4. `src/HealthPlatform.Application/HealthPlatform.Application.csproj` — Add Options package

```xml
<PackageReference Include="Microsoft.Extensions.Options" Version="8.0.0" />
```

### 5. `src/HealthPlatform.Infrastructure/DependencyInjection.cs` — Register options binding

Add using:
```csharp
using HealthPlatform.Application.Settings;
```

Register in `AddInfrastructure`:
```csharp
services.Configure<AccountSecuritySettings>(
    configuration.GetSection(AccountSecuritySettings.SectionName));
```

### 6. `src/HealthPlatform.Infrastructure/HealthPlatform.Infrastructure.csproj` — Add ConfigurationExtensions package

```xml
<PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="8.0.0" />
```

### 7. `src/HealthPlatform.Api/appsettings.json` — Add AccountSecurity section

```json
"AccountSecurity": {
  "MaxFailedLoginAttempts": 5,
  "LockoutDurationMinutes": 15,
  "PasswordExpiryDays": 90,
  "PasswordHistorySize": 5
}
```

### 8. `src/HealthPlatform.Api/appsettings.Development.json` — Mirror settings

Same block as production (no override needed for development).

### 9. Generate EF Core migration

```powershell
cd src
dotnet ef migrations add AddAccountSecurityFields `
  --project HealthPlatform.Infrastructure `
  --startup-project HealthPlatform.Api `
  --context AppDbContext
```

---

## Affected Files

| File | Change |
|------|--------|
| `src/HealthPlatform.Domain/Entities/User.cs` | +4 properties |
| `src/HealthPlatform.Infrastructure/Persistence/Configurations/UserConfiguration.cs` | +4 column mappings |
| `src/HealthPlatform.Application/Settings/AccountSecuritySettings.cs` | **Created** |
| `src/HealthPlatform.Application/HealthPlatform.Application.csproj` | +1 package |
| `src/HealthPlatform.Infrastructure/DependencyInjection.cs` | +options registration |
| `src/HealthPlatform.Infrastructure/HealthPlatform.Infrastructure.csproj` | +1 package |
| `src/HealthPlatform.Api/appsettings.json` | +`AccountSecurity` section |
| `src/HealthPlatform.Api/appsettings.Development.json` | +`AccountSecurity` section |
| `src/HealthPlatform.Infrastructure/Persistence/Migrations/20260527065106_AddAccountSecurityFields.cs` | **Generated** |

---

## Acceptance Criteria

- [ ] `User` entity compiles with 4 new properties
- [ ] `UserConfiguration` maps all 4 columns; `password_history` uses `jsonb` converter
- [ ] `AccountSecuritySettings` has correct defaults (5 / 15 / 90 / 5)
- [ ] Options registered and bound in Infrastructure DI
- [ ] Migration generated without errors; `dotnet build` passes (0 errors)
- [ ] `appsettings.json` contains `AccountSecurity` section

## Verification

```powershell
cd src
dotnet build HealthPlatform.sln --no-restore
```

Expected: `Build succeeded. 0 Error(s)`
