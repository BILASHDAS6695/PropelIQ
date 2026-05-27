using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Patients;

/// <summary>
/// Creates a placeholder User (no portal access) and a PatientProfile
/// for a walk-in patient who is not yet registered in the system.
/// </summary>
internal sealed class QuickCreatePatientCommandHandler
    : IRequestHandler<QuickCreatePatientCommand, QuickCreatePatientResult>
{
    private readonly IUnitOfWork _uow;

    public QuickCreatePatientCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<QuickCreatePatientResult> Handle(
        QuickCreatePatientCommand command,
        CancellationToken         ct)
    {
        // Generate a unique internal email — not used for login.
        var internalEmail = $"walkin-{Guid.NewGuid():N}@internal.local";

        var user = new User
        {
            Id           = Guid.NewGuid(),
            Email        = internalEmail,
            PasswordHash = string.Empty,   // no portal login
            Role         = UserRole.Patient,
            IsActive     = false           // cannot log in
        };

        var profile = new PatientProfile
        {
            Id        = Guid.NewGuid(),
            UserId    = user.Id,
            FirstName = command.FirstName,
            LastName  = command.LastName,
            Dob       = command.Dob,
            Phone     = command.Phone
        };

        await _uow.Repository<User>().AddAsync(user, ct);
        await _uow.Repository<PatientProfile>().AddAsync(profile, ct);
        await _uow.SaveChangesAsync(ct);

        return new QuickCreatePatientResult(profile.Id, user.Id);
    }
}
