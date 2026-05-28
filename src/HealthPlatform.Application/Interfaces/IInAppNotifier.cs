using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Persists an in-app notification to the database and, if the recipient
/// user is connected via SignalR, pushes it in real time.
/// Offline users receive the notification on their next connection
/// (loaded from DB by the Angular client on startup).
/// </summary>
public interface IInAppNotifier
{
    /// <summary>
    /// Persists and optionally pushes a notification to <paramref name="userId"/>.
    /// </summary>
    Task NotifyAsync(
        Guid              userId,
        Guid?             patientId,
        Guid?             appointmentId,
        NotificationType  type,
        string            title,
        string            message,
        string?           actionUrl  = null,
        CancellationToken ct         = default);
}
