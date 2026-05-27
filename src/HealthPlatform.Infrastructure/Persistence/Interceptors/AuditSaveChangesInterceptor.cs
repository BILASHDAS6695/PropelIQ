using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HealthPlatform.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Automatically captures entity-level changes and writes <see cref="AuditLog"/>
/// entries with a SHA-256 hash chain within the same <see cref="DbContext"/>
/// transaction. Skips logging when no authenticated user is present
/// (e.g., startup seeding, background services).
/// Hash formula: SHA256(previousHash + timestamp(ISO-8601) + action + entityId + userId)
/// </summary>
internal sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;

    public AuditSaveChangesInterceptor(ICurrentUserService currentUser)
        => _currentUser = currentUser;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null || !_currentUser.IsAuthenticated)
            return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var db     = eventData.Context;
        var userId = _currentUser.UserId!.Value;
        var entries = BuildAuditEntries(db, userId);

        if (entries.Count > 0)
        {
            var lastHash = await db.Set<AuditLog>()
                .OrderByDescending(a => a.Timestamp)
                .Select(a => a.CurrentHash)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            foreach (var entry in entries)
            {
                var hash = ComputeHash(
                    lastHash, entry.Timestamp, entry.Action,
                    entry.EntityId, entry.UserId);

                entry.PreviousHash = string.IsNullOrEmpty(lastHash) ? null : lastHash;
                entry.CurrentHash  = hash;
                lastHash           = hash;
            }

            await db.Set<AuditLog>().AddRangeAsync(entries, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static List<AuditLog> BuildAuditEntries(DbContext db, Guid userId)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var logs      = new List<AuditLog>();

        foreach (var entry in db.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog) continue;
            if (entry.State is EntityState.Unchanged or EntityState.Detached) continue;

            var action = entry.State switch
            {
                EntityState.Added    => "Created",
                EntityState.Modified => "Updated",
                EntityState.Deleted  => "Deleted",
                _                    => null
            };

            if (action is null) continue;

            var entityId = entry.Properties
                .FirstOrDefault(p => p.Metadata.IsPrimaryKey())
                ?.CurrentValue as Guid? ?? Guid.Empty;

            logs.Add(new AuditLog
            {
                Id         = Guid.NewGuid(),
                UserId     = userId,
                Action     = action,
                EntityType = entry.Entity.GetType().Name,
                EntityId   = entityId,
                Timestamp  = timestamp,
                Details    = BuildDetails(entry, action),
                CurrentHash = string.Empty  // populated by caller
            });
        }

        return logs;
    }

    private static JsonDocument BuildDetails(EntityEntry entry, string action)
    {
        var data = action switch
        {
            "Created" => (object)new
            {
                newValues = entry.Properties
                    .Where(p => !p.Metadata.IsPrimaryKey())
                    .ToDictionary(p => p.Metadata.Name,
                                  p => p.CurrentValue)
            },
            "Updated" => new
            {
                oldValues = entry.Properties
                    .Where(p => p.IsModified)
                    .ToDictionary(p => p.Metadata.Name,
                                  p => p.OriginalValue),
                newValues = entry.Properties
                    .Where(p => p.IsModified)
                    .ToDictionary(p => p.Metadata.Name,
                                  p => p.CurrentValue)
            },
            "Deleted" => new
            {
                oldValues = entry.Properties
                    .Where(p => !p.Metadata.IsPrimaryKey())
                    .ToDictionary(p => p.Metadata.Name,
                                  p => p.OriginalValue)
            },
            _ => new { }
        };

        return JsonDocument.Parse(
            JsonSerializer.Serialize(data,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
    }

    private static string ComputeHash(
        string previousHash,
        DateTimeOffset timestamp,
        string action,
        Guid entityId,
        Guid userId)
    {
        var input = $"{previousHash}{timestamp:O}{action}{entityId}{userId}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
