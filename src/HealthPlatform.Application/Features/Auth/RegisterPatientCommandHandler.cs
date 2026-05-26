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
            Dob       = DateOnly.MinValue, // collected during profile completion, not registration
            Phone     = string.IsNullOrWhiteSpace(request.Phone)
                            ? null
                            : request.Phone.Trim(),
        };

        await _uow.Repository<PatientProfile>().AddAsync(profile, cancellationToken);

        // ── 4. Write AuditLog entry ───────────────────────────────────────
        var auditDetails = JsonSerializer.Serialize(new
        {
            Email     = user.Email,
            FirstName = profile.FirstName,
            LastName  = profile.LastName,
        });

        var auditEntry = new AuditLog
        {
            UserId      = user.Id,
            Action      = "PatientRegistered",
            EntityType  = nameof(User),
            EntityId    = user.Id,
            Timestamp   = DateTimeOffset.UtcNow,
            Details     = JsonDocument.Parse(auditDetails),
            CurrentHash = string.Empty, // hash chaining implemented in a future story
        };

        await _uow.Repository<AuditLog>().AddAsync(auditEntry, cancellationToken);

        // ── 5. Persist all changes in one transaction ─────────────────────
        await _uow.SaveChangesAsync(cancellationToken);

        // ── 6. Send activation email (no-op logging stub for this story) ──
        await _emailSender.SendAsync(
            toAddress : user.Email,
            subject   : "Welcome to HealthPlatform — activate your account",
            body      : $"Hi {profile.FirstName}, your account has been created.",
            ct        : cancellationToken);

        _logger.LogInformation("Patient registered successfully. UserId={UserId}", user.Id);

        return new RegisterPatientResult(true, user.Id, null);
    }
}
