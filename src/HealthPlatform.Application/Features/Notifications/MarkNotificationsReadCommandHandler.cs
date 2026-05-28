using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.Notifications;

internal sealed class MarkNotificationsReadCommandHandler
    : IRequestHandler<MarkNotificationsReadCommand, int>
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;

    public MarkNotificationsReadCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow         = uow;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(
        MarkNotificationsReadCommand command,
        CancellationToken            ct)
    {
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("User must be authenticated.");

        IReadOnlyList<Notification> targets;
        if (command.TargetId is not null)
        {
            targets = await _uow.Repository<Notification>()
                .GetAsync(new SingleUserNotificationSpecification(userId, command.TargetId.Value), ct);
        }
        else
        {
            targets = await _uow.Repository<Notification>()
                .GetAsync(new AllUnreadNotificationsSpecification(userId), ct);
        }

        if (targets.Count == 0)
            return 0;

        var now   = DateTimeOffset.UtcNow;
        int count = 0;
        foreach (var n in targets)
        {
            if (n.IsRead) continue;
            n.IsRead = true;
            n.ReadAt = now;
            _uow.Repository<Notification>().Update(n);
            count++;
        }

        if (count > 0)
            await _uow.SaveChangesAsync(ct);

        return count;
    }
}
