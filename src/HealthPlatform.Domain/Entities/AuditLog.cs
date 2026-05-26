using System.Text.Json;
using HealthPlatform.Domain.Common;

namespace HealthPlatform.Domain.Entities;

public class AuditLog : BaseEntity
{
    public Guid UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public JsonDocument? Details { get; set; }
    public string? PreviousHash { get; set; }
    public string CurrentHash { get; set; } = string.Empty;

    public User User { get; set; } = null!;
}
