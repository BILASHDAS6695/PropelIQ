using HealthPlatform.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HealthPlatform.Api.Hubs;

/// <summary>
/// Real-time notification hub for slot availability and queue status events.
/// Requires an authenticated user (JWT Bearer).
/// Clients join provider-scoped groups to receive targeted broadcasts.
/// </summary>
[Authorize(Policy = PolicyNames.Patient)]
public sealed class NotificationHub : Hub
{
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
}
