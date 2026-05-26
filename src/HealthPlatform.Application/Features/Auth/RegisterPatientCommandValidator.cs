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
