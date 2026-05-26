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
