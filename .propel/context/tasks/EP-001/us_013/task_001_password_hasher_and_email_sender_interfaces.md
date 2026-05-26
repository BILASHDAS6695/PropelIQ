# Task 001: IPasswordHasher and IEmailSender Application Interfaces

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-013 |
| **Epic** | EP-001 |
| **Layer** | Application |
| **Priority** | Critical |
| **Estimated Effort** | 20 minutes |
| **Dependencies** | None |

## Objective

Define two new service-contract interfaces in the Application layer so that the
`RegisterPatientCommandHandler` (Task 003) can hash passwords and dispatch
activation emails without taking a direct dependency on any infrastructure
library.  Following the Dependency Inversion Principle, these contracts live in
`HealthPlatform.Application/Interfaces/` and are resolved at runtime by
infrastructure implementations registered in Task 002.

## Acceptance Criteria Covered

- AC-7: Passwords stored using bcrypt hashing (via the `IPasswordHasher` abstraction)
- AC-5 (partial): Activation email dispatched on successful registration (via `IEmailSender`)

## Implementation Steps

### 1. Add `IPasswordHasher` Interface

Create the file `src/HealthPlatform.Application/Interfaces/IPasswordHasher.cs`:

```csharp
namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Abstracts password hashing so the Application layer never references
/// a specific hashing library.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a plaintext password and returns the hash string.</summary>
    string Hash(string plaintext);

    /// <summary>
    /// Verifies a plaintext password against a previously produced hash.
    /// Returns <c>true</c> when the password matches.
    /// </summary>
    bool Verify(string plaintext, string hash);
}
```

### 2. Add `IEmailSender` Interface

Create the file `src/HealthPlatform.Application/Interfaces/IEmailSender.cs`:

```csharp
namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Abstracts email delivery so the Application layer remains transport-agnostic.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends a single email message.
    /// </summary>
    /// <param name="toAddress">Recipient email address.</param>
    /// <param name="subject">Email subject line.</param>
    /// <param name="body">Plain-text or HTML body of the email.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendAsync(string toAddress, string subject, string body, CancellationToken ct = default);
}
```

## Files Created

| File | Change |
|------|--------|
| `src/HealthPlatform.Application/Interfaces/IPasswordHasher.cs` | New — password hashing contract |
| `src/HealthPlatform.Application/Interfaces/IEmailSender.cs` | New — email delivery contract |

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Debug
# Expect: 0 errors, 0 warnings
```

Confirm both interfaces appear in IntelliSense when typing
`using HealthPlatform.Application.Interfaces;` inside the Application project.

## Notes

- No new NuGet packages are required for this task — interfaces carry no external
  dependencies.
- `IPasswordHasher` deliberately omits ASP.NET Identity references; the
  BCrypt.Net-Next implementation (Task 002) provides the concrete hashing logic.
- `IEmailSender` is transport-agnostic: Task 002 provides a no-op/logging
  stub; a real SMTP or SendGrid implementation can be swapped in later without
  touching the Application layer.
