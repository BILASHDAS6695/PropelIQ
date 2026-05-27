using MediatR;

namespace HealthPlatform.Application.Features.Patients;

/// <summary>
/// Creates a minimal patient record for an unregistered walk-in.
/// Generates a placeholder User (IsActive = false) so the PatientProfile
/// FK constraint is satisfied without issuing portal credentials.
/// </summary>
public sealed record QuickCreatePatientCommand(
    string   FirstName,
    string   LastName,
    DateOnly Dob,
    string?  Phone = null) : IRequest<QuickCreatePatientResult>;

public sealed record QuickCreatePatientResult(
    Guid PatientProfileId,
    Guid UserId);
