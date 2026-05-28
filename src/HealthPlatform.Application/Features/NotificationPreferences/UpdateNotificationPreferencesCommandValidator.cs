using FluentValidation;

namespace HealthPlatform.Application.Features.NotificationPreferences;

internal sealed class UpdateNotificationPreferencesCommandValidator
    : AbstractValidator<UpdateNotificationPreferencesCommand>
{
    public UpdateNotificationPreferencesCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
    }
}
