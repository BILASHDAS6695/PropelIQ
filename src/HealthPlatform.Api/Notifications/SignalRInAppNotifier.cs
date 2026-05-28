using HealthPlatform.Api.Hubs;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Microsoft.AspNetCore.SignalR;

namespace HealthPlatform.Api.Notifications;

/// <summary>
/// <see cref="IInAppNotifier"/> implementation backed by SignalR.
/// Persists a <c>Notification</c> record and then pushes the payload to the
/// user's personal SignalR group (<c>user-{userId}</c>).
/// </summary>
internal sealed class SignalRInAppNotifier : IInAppNotifier
{
    private readonly IHubContext<NotificationHub> _hub;
    private readonly IUnitOfWork                 _uow;

    public SignalRInAppNotifier(IHubContext<NotificationHub> hub, IUnitOfWork uow)
    {
        _hub = hub;
        _uow = uow;
    }

    /// <summary>Returns the SignalR group name for the given user.</summary>
    internal static string UserGroup(Guid userId) => $"user-{userId}";

    public async Task NotifyAsync(
        Guid              userId,
        Guid?             patientId,
        Guid?             appointmentId,
        NotificationType  type,
        string            title,
        string            message,
        string?           actionUrl = null,
        CancellationToken ct        = default)
    {
        var now = DateTimeOffset.UtcNow;

        var notification = new Notification
        {
            Id             = Guid.NewGuid(),
            UserId         = userId,
            PatientId      = patientId,
            AppointmentId  = appointmentId,
            Channel        = NotificationChannel.InApp,
            Type           = type,
            Title          = title,
            Message        = message,
            ActionUrl      = actionUrl,
            DeliveryStatus = DeliveryStatus.Sent,
            SentAt         = now,
            IsRead         = false,
            ExpiresAt      = now.AddDays(90),
        };

        await _uow.Repository<Notification>().AddAsync(notification, ct);
        await _uow.SaveChangesAsync(ct);

        var payload = new InAppNotificationPayload(
            notification.Id,
            type.ToString(),
            title,
            message,
            actionUrl,
            now,
            appointmentId);

        await _hub.Clients
            .Group(UserGroup(userId))
            .SendAsync("Notification", payload, ct);
    }
}
