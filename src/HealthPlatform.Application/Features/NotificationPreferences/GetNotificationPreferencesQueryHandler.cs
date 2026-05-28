using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.NotificationPreferences;

internal sealed class GetNotificationPreferencesQueryHandler
    : IRequestHandler<GetNotificationPreferencesQuery, NotificationPreferencesDto>
{
    private readonly IUnitOfWork _uow;

    public GetNotificationPreferencesQueryHandler(IUnitOfWork uow)
        => _uow = uow;

    public async Task<NotificationPreferencesDto> Handle(
        GetNotificationPreferencesQuery request,
        CancellationToken ct)
    {
        var user = await _uow.Repository<User>().GetByIdAsync(request.UserId, ct)
                   ?? throw new KeyNotFoundException($"User {request.UserId} not found.");

        var p = user.NotificationPreferences;
        return new NotificationPreferencesDto(
            p.EmailReminders,
            p.EmailSwap,
            p.EmailGeneral,
            p.InAppReminders,
            p.InAppSwap,
            p.InAppGeneral);
    }
}
