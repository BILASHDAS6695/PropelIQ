namespace HealthPlatform.Application.Features.Notifications;

public sealed record NotificationDto(
    Guid            Id,
    string          Type,
    string          Channel,
    string          Title,
    string          Message,
    string?         ActionUrl,
    bool            IsRead,
    DateTimeOffset? ReadAt,
    DateTimeOffset  SentAt,
    DateTimeOffset  ExpiresAt,
    Guid?           AppointmentId);
