using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.NotificationPreferences;

internal sealed class UpdateNotificationPreferencesCommandHandler
    : IRequestHandler<UpdateNotificationPreferencesCommand>
{
    private readonly IUnitOfWork _uow;

    public UpdateNotificationPreferencesCommandHandler(IUnitOfWork uow)
        => _uow = uow;

    public async Task Handle(
        UpdateNotificationPreferencesCommand request,
        CancellationToken ct)
    {
        var user = await _uow.Repository<User>().GetByIdAsync(request.UserId, ct)
                   ?? throw new KeyNotFoundException($"User {request.UserId} not found.");

        user.NotificationPreferences.EmailReminders = request.EmailReminders;
        user.NotificationPreferences.EmailSwap      = request.EmailSwap;
        user.NotificationPreferences.EmailGeneral   = request.EmailGeneral;
        user.NotificationPreferences.InAppReminders = request.InAppReminders;
        user.NotificationPreferences.InAppSwap      = request.InAppSwap;
        user.NotificationPreferences.InAppGeneral   = request.InAppGeneral;

        _uow.Repository<User>().Update(user);
        await _uow.SaveChangesAsync(ct);
    }
}
