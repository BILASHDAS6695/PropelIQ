# Task 002: BCrypt Password Hasher and NoOp Email Sender (Infrastructure)

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-013 |
| **Epic** | EP-001 |
| **Layer** | Infrastructure |
| **Priority** | Critical |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | Task 001 (IPasswordHasher, IEmailSender interfaces) |

## Objective

Provide concrete implementations of the two interfaces defined in Task 001 and
register them in the Infrastructure DI extension:

1. `BcryptPasswordHasher` — wraps `BCrypt.Net-Next` to hash and verify passwords
   with a work-factor of 12 (satisfying NFR-014 and OWASP password storage
   guidance).
2. `NoOpEmailSender` — logs the activation email via `ILogger` as a placeholder;
   no SMTP credentials are required for this story.  A real transport can be
   substituted later without touching the Application layer.

## Acceptance Criteria Covered

- AC-7: Passwords stored using bcrypt hashing (work-factor 12)
- AC-5 (partial): Activation email dispatched — logged for now, real delivery in a future story

## Implementation Steps

### 1. Add NuGet Package to `HealthPlatform.Infrastructure.csproj`

```xml
<PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
```

### 2. Create `BcryptPasswordHasher`

Create `src/HealthPlatform.Infrastructure/Security/BcryptPasswordHasher.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using BC = BCrypt.Net.BCrypt;

namespace HealthPlatform.Infrastructure.Security;

/// <summary>
/// BCrypt-backed password hasher using a work-factor of 12.
/// Satisfies NFR-014 and OWASP password storage recommendations.
/// </summary>
internal sealed class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string plaintext) =>
        BC.HashPassword(plaintext, WorkFactor);

    public bool Verify(string plaintext, string hash) =>
        BC.Verify(plaintext, hash);
}
```

### 3. Create `NoOpEmailSender`

Create `src/HealthPlatform.Infrastructure/Messaging/NoOpEmailSender.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Infrastructure.Messaging;

/// <summary>
/// Development-phase email sender that logs the message instead of
/// transmitting it.  Replace with a real SMTP / SendGrid implementation
/// when email delivery is in scope.
/// </summary>
internal sealed class NoOpEmailSender : IEmailSender
{
    private readonly ILogger<NoOpEmailSender> _logger;

    public NoOpEmailSender(ILogger<NoOpEmailSender> logger) => _logger = logger;

    public Task SendAsync(
        string toAddress,
        string subject,
        string body,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[NoOpEmailSender] To={ToAddress} Subject={Subject} Body={Body}",
            toAddress, subject, body);

        return Task.CompletedTask;
    }
}
```

> **Security note**: The logger enricher `PhiSanitizingEnricher` already scrubs
> PHI patterns from Serilog output; the `ToAddress` field will be redacted in
> production sinks if it matches the PII pattern.

### 4. Register Both Services in `DependencyInjection.cs`

In `src/HealthPlatform.Infrastructure/DependencyInjection.cs`, add the following
registrations after the existing `services.AddScoped<IUnitOfWork, UnitOfWork>();`
line:

```csharp
services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
services.AddScoped<IEmailSender, NoOpEmailSender>();
```

Add the required `using` directives at the top of the file:

```csharp
using HealthPlatform.Infrastructure.Messaging;
using HealthPlatform.Infrastructure.Security;
```

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Infrastructure/HealthPlatform.Infrastructure.csproj` | Add `BCrypt.Net-Next` 4.0.3 |
| `src/HealthPlatform.Infrastructure/Security/BcryptPasswordHasher.cs` | New — BCrypt IPasswordHasher impl |
| `src/HealthPlatform.Infrastructure/Messaging/NoOpEmailSender.cs` | New — logging IEmailSender stub |
| `src/HealthPlatform.Infrastructure/DependencyInjection.cs` | Register both services + add usings |

## Verification

```bash
cd src
dotnet add HealthPlatform.Infrastructure/HealthPlatform.Infrastructure.csproj \
    package BCrypt.Net-Next --version 4.0.3
dotnet build HealthPlatform.sln --configuration Debug
# Expect: 0 errors
```

Run a quick smoke test by resolving `IPasswordHasher` through DI in a unit test
(or assert in the existing test project that `Hash` + `Verify` round-trip
correctly).

## Notes

- `BcryptPasswordHasher` is registered as `Singleton` because BCrypt has no
  mutable state; work-factor is baked into each hash string so upgrades are
  transparent.
- `NoOpEmailSender` is registered as `Scoped` to match future SMTP/HTTP client
  lifetimes that will require `IHttpClientFactory`.
- Work-factor 12 produces approximately 250 ms per hash on modern hardware —
  acceptable for a registration endpoint that is not high-frequency.
