# Task 003: SignalR NotificationHub

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-007 |
| **Epic** | EP-TECH |
| **Layer** | API |
| **Priority** | High |
| **Estimated Effort** | 1 hour |
| **Dependencies** | Task 002 (JWT Bearer auth must be registered first) |

## Objective

Create the `NotificationHub` and configure SignalR so that:

1. The hub is reachable at `/hubs/notifications`.
2. `SlotAvailabilityChanged` and `QueueStatusUpdated` event methods are defined
   with strongly-typed payloads.
3. Only authenticated users may connect (`[Authorize]`).
4. WebSocket is the primary transport; long-polling is the automatic fallback
   provided by the SignalR runtime.
5. Group-based messaging allows per-provider targeted broadcasts.

`Microsoft.AspNetCore.SignalR` ships in the ASP.NET Core shared framework —
no additional NuGet package is needed for .NET 8.

## Acceptance Criteria Covered

- AC-4: SignalR hub configured at `/hubs/notifications`
- AC-5: Hub supports `SlotAvailabilityChanged`, `QueueStatusUpdated` events
- AC-6: SignalR authentication validates JWT tokens (enforced by `[Authorize]`)
- AC-7: WebSocket transport with long-polling fallback configured

## Implementation Steps

### 1. Create Strongly-Typed Payload Records

Create the file `src/HealthPlatform.Api/Hubs/NotificationModels.cs`:

```csharp
namespace HealthPlatform.Api.Hubs;

/// <summary>Payload broadcast when a provider's slot availability changes.</summary>
public sealed record SlotAvailabilityChangedPayload(
    Guid ProviderId,
    DateOnly Date,
    int AvailableSlots);

/// <summary>Payload broadcast when a provider's queue status is updated.</summary>
public sealed record QueueStatusUpdatedPayload(
    Guid ProviderId,
    int QueueLength,
    int EstimatedWaitMinutes);
```

### 2. Create `NotificationHub`

Create the file `src/HealthPlatform.Api/Hubs/NotificationHub.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HealthPlatform.Api.Hubs;

/// <summary>
/// Real-time notification hub for slot availability and queue status events.
/// Requires an authenticated user (JWT Bearer).
/// Clients join provider-scoped groups to receive targeted broadcasts.
/// </summary>
[Authorize]
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
```

> **Server-side broadcast helpers** (called from Application/Infrastructure
> services) use `IHubContext<NotificationHub>` injected via DI:
>
> ```csharp
> // Example usage in a domain event handler:
> await _hub.Clients
>     .Group($"provider-{providerId}")
>     .SendAsync("SlotAvailabilityChanged", payload, cancellationToken);
>
> await _hub.Clients
>     .Group($"provider-{providerId}")
>     .SendAsync("QueueStatusUpdated", payload, cancellationToken);
> ```

### 3. Register SignalR in `Program.cs`

Add after `builder.Services.AddAuthorization()`:

```csharp
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});
```

### 4. Map the Hub Endpoint in `Program.cs`

Add after `app.MapControllers()`:

```csharp
app.MapHub<NotificationHub>("/hubs/notifications");
```

The SignalR runtime automatically negotiates the best available transport
(WebSocket → Server-Sent Events → Long Polling) satisfying AC-7.

### 5. Add `using` Directive to `Program.cs`

```csharp
using HealthPlatform.Api.Hubs;
```

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Api/Hubs/NotificationModels.cs` | New — payload records |
| `src/HealthPlatform.Api/Hubs/NotificationHub.cs` | New — hub implementation |
| `src/HealthPlatform.Api/Program.cs` | Add `AddSignalR()`, `MapHub<NotificationHub>()`, using directive |

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Release
dotnet test HealthPlatform.sln --no-build --configuration Release
# Optional: connect with wscat or SignalR JS client to wss://localhost:{port}/hubs/notifications
# Confirm unauthenticated connection is rejected with 401.
```

## Notes

- `EnableDetailedErrors = true` in Development surfaces hub exception details
  to the client, aiding local debugging without leaking info in production.
- Group naming convention `provider-{providerId}` is intentional — keeps groups
  namespaced and avoids collisions with future hub expansions.
- `IHubContext<NotificationHub>` is automatically registered by `AddSignalR()`
  and can be injected into Application layer handlers without any additional
  registration.
- The JWT token extraction from query string (`OnMessageReceived` in Task 002)
  covers the WebSocket browser limitation where custom headers cannot be set.
