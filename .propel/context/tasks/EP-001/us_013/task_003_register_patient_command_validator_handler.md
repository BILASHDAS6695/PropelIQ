# Task 003: RegisterPatient CQRS Command, Validator, and Handler (Application Layer)

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-013 |
| **Epic** | EP-001 |
| **Layer** | Application |
| **Priority** | Critical |
| **Estimated Effort** | 60 minutes |
| **Dependencies** | Task 001 (interfaces), Task 002 (implementations registered in DI) |

## Objective

Implement the core business logic for patient self-registration as a MediatR
CQRS command.  This task creates four artefacts inside
`src/HealthPlatform.Application/Features/Auth/`:

1. `RegisterPatientCommand` — the MediatR request and result records.
2. `RegisterPatientCommandValidator` — FluentValidation rules covering all
   acceptance-criteria field constraints.
3. `UserByEmailSpecification` — a query specification for email-uniqueness
   checking without exposing raw LINQ outside the infrastructure layer.
4. `RegisterPatientCommandHandler` — orchestrates User creation, PatientProfile
   creation, AuditLog entry, and activation email dispatch within a single
   unit-of-work transaction.

## Acceptance Criteria Covered

- AC-1: Registration form fields collected (email, firstName, lastName, phone, password, confirmPassword)
- AC-2: Email validated for format and uniqueness (case-insensitive)
- AC-3: Password complexity enforced (12+ chars, uppercase, lowercase, digit, special character)
- AC-4: Phone number validated for format
- AC-5: User created with Role = Patient, PatientProfile created, activation email sent
- AC-6: Duplicate email → domain-level conflict detected, result carries error flag
- AC-7: Password stored via `IPasswordHasher` (BCrypt)
- AC-8: Registration action logged in AuditLog
- AC-9: Returns user ID (no password in result)

## Implementation Steps

### 1. Create Directory Structure

```
src/HealthPlatform.Application/Features/Auth/
```

### 2. Create Command and Result Records

Create `src/HealthPlatform.Application/Features/Auth/RegisterPatientCommand.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Auth;

/// <summary>Patient self-registration request.</summary>
public sealed record RegisterPatientCommand(
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    string Password,
    string ConfirmPassword
) : IRequest<RegisterPatientResult>;

/// <summary>
/// Outcome of a registration attempt.
/// <c>IsSuccess</c> is <c>false</c> when the email is already registered.
/// </summary>
public sealed record RegisterPatientResult(
    bool IsSuccess,
    Guid? UserId,
    string? Error
);
```

### 3. Create `UserByEmailSpecification`

Create `src/HealthPlatform.Application/Features/Auth/UserByEmailSpecification.cs`:

```csharp
using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Auth;

/// <summary>
/// Matches a single <see cref="User"/> by email address (case-insensitive).
/// Used by <see cref="RegisterPatientCommandHandler"/> to detect duplicate registrations.
/// </summary>
internal sealed class UserByEmailSpecification : ISpecification<User>
{
    private readonly string _email;

    public UserByEmailSpecification(string email) =>
        _email = email.ToLowerInvariant();

    public Expression<Func<User, bool>>? Criteria =>
        u => u.Email.ToLower() == _email;

    public List<Expression<Func<User, object>>> Includes => [];
    public Expression<Func<User, object>>? OrderBy => null;
    public Expression<Func<User, object>>? OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int Skip => 0;
    public int Take => 0;
}
```

### 4. Create `RegisterPatientCommandValidator`

Create `src/HealthPlatform.Application/Features/Auth/RegisterPatientCommandValidator.cs`:

```csharp
using FluentValidation;

namespace HealthPlatform.Application.Features.Auth;

/// <summary>
/// Enforces all field-level constraints from US-013 acceptance criteria.
/// Runs automatically via <see cref="HealthPlatform.Application.Behaviors.ValidationBehavior{TRequest,TResponse}"/>.
/// </summary>
public sealed class RegisterPatientCommandValidator
    : AbstractValidator<RegisterPatientCommand>
{
    // Phone: optional; when present must be E.164-compatible
    // (optional leading +, 7–15 digits)
    private const string PhonePattern = @"^\+?[0-9]{7,15}$";

    // Password complexity: 12+ chars, ≥1 uppercase, ≥1 lowercase,
    // ≥1 digit, ≥1 special character (NFR-014)
    private const string PasswordPattern =
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).{12,}$";

    public RegisterPatientCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.")
            .EmailAddress().WithMessage("Email format is invalid.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters.");

        RuleFor(x => x.Phone)
            .Matches(PhonePattern)
            .WithMessage("Phone number format is invalid. Use digits with an optional leading '+'.")
            .When(x => !string.IsNullOrEmpty(x.Phone));

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .Matches(PasswordPattern)
            .WithMessage(
                "Password must be at least 12 characters and include an uppercase letter, " +
                "a lowercase letter, a digit, and a special character.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Please confirm your password.")
            .Equal(x => x.Password).WithMessage("Passwords do not match.");
    }
}
```

### 5. Create `RegisterPatientCommandHandler`

Create `src/HealthPlatform.Application/Features/Auth/RegisterPatientCommandHandler.cs`:

```csharp
using System.Text.Json;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Application.Features.Auth;

/// <summary>
/// Handles patient self-registration.
/// On success: persists <see cref="User"/> + <see cref="PatientProfile"/> +
/// <see cref="AuditLog"/> in one transaction, then sends an activation email.
/// On duplicate email: returns a failure result without throwing.
/// </summary>
internal sealed class RegisterPatientCommandHandler
    : IRequestHandler<RegisterPatientCommand, RegisterPatientResult>
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<RegisterPatientCommandHandler> _logger;

    public RegisterPatientCommandHandler(
        IUnitOfWork uow,
        IPasswordHasher hasher,
        IEmailSender emailSender,
        ILogger<RegisterPatientCommandHandler> logger)
    {
        _uow         = uow;
        _hasher      = hasher;
        _emailSender = emailSender;
        _logger      = logger;
    }

    public async Task<RegisterPatientResult> Handle(
        RegisterPatientCommand request,
        CancellationToken cancellationToken)
    {
        // ── 1. Email-uniqueness check (case-insensitive) ──────────────────
        var userRepo = _uow.Repository<User>();
        var spec     = new UserByEmailSpecification(request.Email);
        var existing = await userRepo.GetAsync(spec, cancellationToken);

        if (existing.Count > 0)
        {
            _logger.LogWarning("Registration attempted with duplicate email.");
            return new RegisterPatientResult(false, null,
                "An account with this email already exists.");
        }

        // ── 2. Create User ────────────────────────────────────────────────
        var user = new User
        {
            Email        = request.Email.ToLowerInvariant(),
            PasswordHash = _hasher.Hash(request.Password),
            Role         = UserRole.Patient,
            IsActive     = true,
        };

        await userRepo.AddAsync(user, cancellationToken);

        // ── 3. Create PatientProfile ──────────────────────────────────────
        var profile = new PatientProfile
        {
            UserId    = user.Id,
            FirstName = request.FirstName.Trim(),
            LastName  = request.LastName.Trim(),
            Phone     = string.IsNullOrWhiteSpace(request.Phone)
                            ? null
                            : request.Phone.Trim(),
        };

        await _uow.Repository<PatientProfile>().AddAsync(profile, cancellationToken);

        // ── 4. Write AuditLog entry ───────────────────────────────────────
        var auditEntry = new AuditLog
        {
            UserId      = user.Id,
            Action      = "PatientRegistered",
            EntityType  = nameof(User),
            EntityId    = user.Id,
            Timestamp   = DateTimeOffset.UtcNow,
            Details     = JsonDocument.Parse(
                              JsonSerializer.Serialize(new
                              {
                                  Email     = user.Email,
                                  FirstName = profile.FirstName,
                                  LastName  = profile.LastName,
                              })),
            CurrentHash = string.Empty, // hash chaining implemented in US-014
        };

        await _uow.Repository<AuditLog>().AddAsync(auditEntry, cancellationToken);

        // ── 5. Persist all changes in one transaction ─────────────────────
        await _uow.SaveChangesAsync(cancellationToken);

        // ── 6. Send activation email (fire-and-forget logging stub) ───────
        await _emailSender.SendAsync(
            toAddress : user.Email,
            subject   : "Welcome to HealthPlatform — activate your account",
            body      : $"Hi {profile.FirstName}, your account has been created.",
            ct        : cancellationToken);

        _logger.LogInformation("Patient registered successfully. UserId={UserId}", user.Id);

        return new RegisterPatientResult(true, user.Id, null);
    }
}
```

> **Security note**: The `Email` field in `AuditLog.Details` is normalised to
> lowercase before persistence; the `PhiSanitizingEnricher` will further scrub
> it from log sinks.  The password hash is never stored in the audit details.

## Files Created

| File | Change |
|------|--------|
| `src/HealthPlatform.Application/Features/Auth/RegisterPatientCommand.cs` | New — command + result records |
| `src/HealthPlatform.Application/Features/Auth/UserByEmailSpecification.cs` | New — email-uniqueness specification |
| `src/HealthPlatform.Application/Features/Auth/RegisterPatientCommandValidator.cs` | New — FluentValidation rules |
| `src/HealthPlatform.Application/Features/Auth/RegisterPatientCommandHandler.cs` | New — MediatR request handler |

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Debug
# Expect: 0 errors
```

Run unit tests for the validator:

```bash
dotnet test HealthPlatform.Tests --filter "Category=Auth"
```

Manual verification:
1. Register with a valid payload → expect `IsSuccess = true` and a non-null `UserId`.
2. Register again with the same email → expect `IsSuccess = false`, `Error = "An account with this email already exists."`.
3. Register with `Password = "short"` → `ValidationBehavior` raises `ValidationException` before the handler executes.

## Notes

- `ValidationBehavior<TRequest, TResponse>` (already in the pipeline) runs
  `RegisterPatientCommandValidator` automatically before `Handle` is called, so
  the handler can assume all field constraints are satisfied.
- `AuditLog.CurrentHash` is left as `string.Empty` in this story; hash-chaining
  is addressed in a dedicated tech story.
- The `PatientProfile.Dob` property is required by the domain entity but is not
  collected on the registration form (US-013 scope). Set it to
  `DateOnly.MinValue` as a placeholder; the patient can update it in their profile.
  Update the handler if `Dob` becomes required at registration in a later story.
