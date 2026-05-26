using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HealthPlatform.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Appends an immutable <see cref="AuditLog"/> row for every entity change after the
/// main transaction commits. Uses SHA-256 hash chaining for tamper-evidence (HIPAA DR-016).
/// </summary>
public sealed class AuditInterceptor : ISaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    // Pending entries built from the change tracker BEFORE the main commit.
    // Entity states and original values are still valid at that point.
    private readonly List<(string EntityType, Guid EntityId, AuditAction Action, string Details)> _pending = [];

    public AuditInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // -------------------------------------------------------------------------
    // SavingChanges — capture entries BEFORE commit while states are valid
    // -------------------------------------------------------------------------

    public ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CapturePendingEntries(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        CapturePendingEntries(eventData.Context);
        return result;
    }

    // -------------------------------------------------------------------------
    // SavedChanges — flush captured entries AFTER the main transaction commits
    // -------------------------------------------------------------------------

    public async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null && _pending.Count > 0)
            await FlushAuditEntriesAsync(eventData.Context, cancellationToken);
        return result;
    }

    public int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is not null && _pending.Count > 0)
            FlushAuditEntriesAsync(eventData.Context, CancellationToken.None)
                .GetAwaiter().GetResult();
        return result;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void CapturePendingEntries(DbContext? context)
    {
        if (context is null) return;

        _pending.Clear();
        context.ChangeTracker.DetectChanges();

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.Entity is AuditLog) continue; // prevent self-auditing

            var action = entry.State switch
            {
                EntityState.Added    => (AuditAction?)AuditAction.Created,
                EntityState.Modified => AuditAction.Updated,
                EntityState.Deleted  => AuditAction.Deleted,
                _                    => null
            };

            if (action is null) continue;

            _pending.Add((
                entry.Entity.GetType().Name,
                entry.Entity.Id,
                action.Value,
                BuildDetails(entry, action.Value)
            ));
        }
    }

    private async Task FlushAuditEntriesAsync(DbContext context, CancellationToken ct)
    {
        var userId       = ResolveUserId();
        var now          = DateTimeOffset.UtcNow;
        var previousHash = await GetLastHashAsync(context, ct);

        var auditLogs = new List<AuditLog>(_pending.Count);

        foreach (var (entityType, entityId, action, details) in _pending)
        {
            var currentHash = ComputeHash(previousHash, now, action, entityId, userId);

            auditLogs.Add(new AuditLog
            {
                Id           = Guid.NewGuid(),
                UserId       = userId,
                Action       = action,
                EntityType   = entityType,
                EntityId     = entityId,
                Timestamp    = now,
                Details      = details,
                PreviousHash = previousHash,
                CurrentHash  = currentHash
            });

            previousHash = currentHash; // advance the hash chain
        }

        // Clear _pending BEFORE the inner SaveChangesAsync so CapturePendingEntries
        // sees an empty list on the recursive call — preventing infinite recursion.
        _pending.Clear();

        context.Set<AuditLog>().AddRange(auditLogs);
        await context.SaveChangesAsync(ct);
    }

    private static string BuildDetails(EntityEntry<BaseEntity> entry, AuditAction action)
    {
        var changes = new Dictionary<string, object?>();

        if (action == AuditAction.Created)
        {
            foreach (var prop in entry.Properties)
                changes[prop.Metadata.Name] = new { New = prop.CurrentValue };
        }
        else if (action == AuditAction.Updated)
        {
            foreach (var prop in entry.Properties.Where(p => p.IsModified))
                changes[prop.Metadata.Name] = new { Old = prop.OriginalValue, New = prop.CurrentValue };
        }
        else // Deleted
        {
            foreach (var prop in entry.Properties)
                changes[prop.Metadata.Name] = new { Old = prop.OriginalValue };
        }

        return JsonSerializer.Serialize(changes);
    }

    private static async Task<string?> GetLastHashAsync(DbContext context, CancellationToken ct) =>
        await context.Set<AuditLog>()
            .OrderByDescending(a => a.Timestamp)
            .Select(a => a.CurrentHash)
            .FirstOrDefaultAsync(ct);

    private static string ComputeHash(
        string? previousHash,
        DateTimeOffset timestamp,
        AuditAction action,
        Guid entityId,
        Guid? userId)
    {
        // ADR-006: SHA256( PreviousHash + Timestamp + Action + EntityId + UserId )
        var raw   = $"{previousHash}{timestamp:O}{action}{entityId}{userId}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private Guid? ResolveUserId()
    {
        var claim = _httpContextAccessor.HttpContext?
            .User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
