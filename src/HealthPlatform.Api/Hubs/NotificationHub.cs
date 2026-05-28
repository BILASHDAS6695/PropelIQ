using HealthPlatform.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace HealthPlatform.Api.Hubs;

/// <summary>
/// Real-time notification hub for slot availability, queue status, and in-app notification events.
/// Requires an authenticated user (JWT Bearer).
/// On connection each user is automatically joined to their personal <c>user-{userId}</c> group.
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub
{
    /// <summary>Returns the SignalR group name for the given user's personal channel.</summary>
    public static string UserGroup(Guid userId) => $"user-{userId}";

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        var raw = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (raw is not null && Guid.TryParse(raw, out var userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));

        await base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var raw = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (raw is not null && Guid.TryParse(raw, out var userId))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, UserGroup(userId));

        await base.OnDisconnectedAsync(exception);
    }


    /// <summary>
    /// Subscribes the calling connection to a provider-scoped group so that
    /// subsequent broadcasts to that group are delivered to this client.
    /// </summary>
    /// <param name="providerId">The provider whose updates should be received.</param>
    public async Task SubscribeToProvider(string providerId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"provider-{providerId}");
    }

    /// <summary>
    /// Removes the calling connection from a provider-scoped group.
    /// </summary>
    /// <param name="providerId">The provider to unsubscribe from.</param>
    public async Task UnsubscribeFromProvider(string providerId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"provider-{providerId}");
    }

    /// <summary>
    /// Subscribes the calling connection to the clinic-wide staff notifications
    /// group so that override events and conflict alerts are delivered.
    /// Requires Staff or Admin role.
    /// </summary>
    [Authorize(Policy = PolicyNames.Staff)]
    public async Task SubscribeToStaffNotifications()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "staff-notifications");
    }

    /// <summary>
    /// Removes the calling connection from the staff notifications group.
    /// </summary>
    [Authorize(Policy = PolicyNames.Staff)]
    public async Task UnsubscribeFromStaffNotifications()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "staff-notifications");
    }
}
