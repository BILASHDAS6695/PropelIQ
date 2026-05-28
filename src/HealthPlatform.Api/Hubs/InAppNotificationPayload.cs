namespace HealthPlatform.Api.Hubs;

/// <summary>
/// Payload pushed to SignalR clients on the <c>Notification</c> event.
/// </summary>
public sealed record InAppNotificationPayload(
    Guid            Id,
    string          Type,
    string          Title,
    string          Message,
    string?         ActionUrl,
    DateTimeOffset  SentAt,
    Guid?           AppointmentId);
