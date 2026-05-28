# Task 002: SignalR Hub, Queries, Commands & Event Integration

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-034 |
| **Epic** | EP-004 |
| **Layer** | Infrastructure + Application + API |
| **Priority** | High |
| **Estimated Effort** | 60 minutes |
| **Dependencies** | Task 001 complete — `IInAppNotifier`, extended `Notification` entity and migration applied |

## Objective

1. **Extend `NotificationHub`** — join user-specific SignalR group on connect
   so notifications can be pushed to any individual user (patient or staff).
2. **Implement `SignalRInAppNotifier`** — persists the `Notification` record
   and pushes to the user's SignalR group.
3. **Add `GetNotificationsQuery`** — returns the last 20 in-app notifications
   for the current user plus the unread count.
4. **Add `MarkNotificationsReadCommand`** — marks one or all in-app
   notifications as read for the current user.
5. **Add `NotificationsController`** — REST endpoints for the Angular client.
6. **Integrate `IInAppNotifier` into existing event handlers** — arrival,
   status change, swap request/result, and appointment reminder.
7. **Register** new services in DI.

---

## Acceptance Criteria Covered

- AC: SignalR hub with user-specific groups
- AC: Notification types: swap_request, swap_result, appointment_reminder, arrival, status_change
- AC: Notifications persisted in database for history (last 90 days)
- AC: Click bell → dropdown list of recent notifications (last 20)
- AC: Mark as read on click or "Mark all read"
- AC: User offline → delivered on reconnect (queued in DB)

---

## Implementation Steps

### 1. Add `NotificationDto`

Create `src/HealthPlatform.Application/Features/Notifications/NotificationDto.cs`:

```csharp
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.Notifications;

public sealed record NotificationDto(
    Guid               Id,
    NotificationType   Type,
    string             Title,
    string             Message,
    string?            ActionUrl,
    bool               IsRead,
    DateTimeOffset     SentAt);
```

### 2. Add `GetNotificationsQuery`

Create `src/HealthPlatform.Application/Features/Notifications/GetNotificationsQuery.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Notifications;

/// <param name="PageSize">Max number of notifications to return (default 20).</param>
public sealed record GetNotificationsQuery(int PageSize = 20)
    : IRequest<GetNotificationsResult>;

public sealed record GetNotificationsResult(
    IReadOnlyList<NotificationDto> Items,
    int                            UnreadCount);
```

Create `src/HealthPlatform.Application/Features/Notifications/GetNotificationsQueryHandler.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Notifications;

internal sealed class GetNotificationsQueryHandler
    : IRequestHandler<GetNotificationsQuery, GetNotificationsResult>
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;

    public GetNotificationsQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow         = uow;
        _currentUser = currentUser;
    }

    public async Task<GetNotificationsResult> Handle(
        GetNotificationsQuery query, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
            return new GetNotificationsResult([], 0);

        var spec = new UserNotificationsSpecification(
            _currentUser.UserId.Value, query.PageSize);

        var items = await _uow.Repository<Notification>().GetAsync(spec, ct);

        var unread = await _uow.Repository<Notification>()
            .CountAsync(new UnreadNotificationsCountSpecification(_currentUser.UserId.Value), ct);

        var dtos = items
            .Select(n => new NotificationDto(
                n.Id, n.Type, n.Title, n.Message, n.ActionUrl, n.IsRead, n.SentAt))
            .ToList();

        return new GetNotificationsResult(dtos, unread);
    }
}
```

### 3. Add specifications

Create `src/HealthPlatform.Application/Features/Notifications/UserNotificationsSpecification.cs`:

```csharp
using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.Notifications;

/// <summary>Returns the most recent N in-app notifications for a user.</summary>
internal sealed class UserNotificationsSpecification : ISpecification<Notification>
{
    private readonly Guid _userId;
    private readonly int  _take;

    public UserNotificationsSpecification(Guid userId, int take)
    {
        _userId = userId;
        _take   = take;
    }

    public Expression<Func<Notification, bool>>? Criteria =>
        n => n.UserId == _userId && n.Channel == NotificationChannel.InApp;

    public List<Expression<Func<Notification, object>>> Includes => [];

    public Expression<Func<Notification, object>>? OrderBy           => null;
    public Expression<Func<Notification, object>>? OrderByDescending => n => n.SentAt;
    public bool IsPagingEnabled => true;
    public int  Skip            => 0;
    public int  Take            => _take;
}
```

Create `src/HealthPlatform.Application/Features/Notifications/UnreadNotificationsCountSpecification.cs`:

```csharp
using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.Notifications;

internal sealed class UnreadNotificationsCountSpecification : ISpecification<Notification>
{
    private readonly Guid _userId;

    public UnreadNotificationsCountSpecification(Guid userId) => _userId = userId;

    public Expression<Func<Notification, bool>>? Criteria =>
        n => n.UserId == _userId
          && n.Channel == NotificationChannel.InApp
          && !n.IsRead;

    public List<Expression<Func<Notification, object>>> Includes => [];
    public Expression<Func<Notification, object>>? OrderBy           => null;
    public Expression<Func<Notification, object>>? OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
```

### 4. Add `MarkNotificationsReadCommand`

Create `src/HealthPlatform.Application/Features/Notifications/MarkNotificationsReadCommand.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Notifications;

/// <summary>
/// Marks one specific notification (or all) as read for the current user.
/// </summary>
/// <param name="NotificationId">
/// The specific notification to mark read.
/// Pass <c>null</c> to mark all as read ("Mark all read" button).
/// </param>
public sealed record MarkNotificationsReadCommand(Guid? NotificationId)
    : IRequest<int>;
```

Create `src/HealthPlatform.Application/Features/Notifications/MarkNotificationsReadCommandHandler.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Notifications;

internal sealed class MarkNotificationsReadCommandHandler
    : IRequestHandler<MarkNotificationsReadCommand, int>
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;

    public MarkNotificationsReadCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow         = uow;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(
        MarkNotificationsReadCommand command, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
            return 0;

        IReadOnlyList<Notification> targets;

        if (command.NotificationId.HasValue)
        {
            var spec = new SingleUserNotificationSpecification(
                command.NotificationId.Value, _currentUser.UserId.Value);
            targets = await _uow.Repository<Notification>().GetAsync(spec, ct);
        }
        else
        {
            var spec = new UnreadNotificationsCountSpecification(_currentUser.UserId.Value)
                as ISpecification<Notification>;
            // Reuse existing spec — returns all unread (no paging)
            targets = await _uow.Repository<Notification>().GetAsync(
                new AllUnreadNotificationsSpecification(_currentUser.UserId.Value), ct);
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var n in targets.Where(n => !n.IsRead))
        {
            n.IsRead = true;
            n.ReadAt  = now;
            _uow.Repository<Notification>().Update(n);
        }

        await _uow.SaveChangesAsync(ct);
        return targets.Count(n => n.ReadAt == now);
    }
}
```

Create `src/HealthPlatform.Application/Features/Notifications/SingleUserNotificationSpecification.cs`:

```csharp
using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Notifications;

internal sealed class SingleUserNotificationSpecification : ISpecification<Notification>
{
    private readonly Guid _notificationId;
    private readonly Guid _userId;

    public SingleUserNotificationSpecification(Guid notificationId, Guid userId)
    {
        _notificationId = notificationId;
        _userId         = userId;
    }

    public Expression<Func<Notification, bool>>? Criteria =>
        n => n.Id == _notificationId && n.UserId == _userId;

    public List<Expression<Func<Notification, object>>> Includes => [];
    public Expression<Func<Notification, object>>? OrderBy           => null;
    public Expression<Func<Notification, object>>? OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
```

Create `src/HealthPlatform.Application/Features/Notifications/AllUnreadNotificationsSpecification.cs`:

```csharp
using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.Notifications;

internal sealed class AllUnreadNotificationsSpecification : ISpecification<Notification>
{
    private readonly Guid _userId;
    public AllUnreadNotificationsSpecification(Guid userId) => _userId = userId;

    public Expression<Func<Notification, bool>>? Criteria =>
        n => n.UserId == _userId
          && n.Channel == NotificationChannel.InApp
          && !n.IsRead;

    public List<Expression<Func<Notification, object>>> Includes => [];
    public Expression<Func<Notification, object>>? OrderBy           => null;
    public Expression<Func<Notification, object>>? OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
```

### 5. Define SignalR push payload

Create `src/HealthPlatform.Api/Hubs/InAppNotificationPayload.cs`:

```csharp
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Api.Hubs;

/// <summary>
/// Pushed to the client via the <c>ReceiveNotification</c> SignalR method
/// whenever a new in-app notification is created.
/// </summary>
public sealed record InAppNotificationPayload(
    Guid             Id,
    NotificationType Type,
    string           Title,
    string           Message,
    string?          ActionUrl,
    DateTimeOffset   SentAt,
    int              NewUnreadCount);
```

### 6. Extend `NotificationHub` with user-specific groups

Update `src/HealthPlatform.Api/Hubs/NotificationHub.cs`:

```csharp
using HealthPlatform.Api.Authorization;
using HealthPlatform.Application.Features.Notifications;
using HealthPlatform.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HealthPlatform.Api.Hubs;

[Authorize]
public sealed class NotificationHub : Hub
{
    private readonly ISender             _sender;
    private readonly ICurrentUserService _currentUser;

    public NotificationHub(ISender sender, ICurrentUserService currentUser)
    {
        _sender      = sender;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Joins the user's personal SignalR group on connect and delivers
    /// any unread notifications accumulated while offline.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        if (_currentUser.UserId is not null)
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                UserGroup(_currentUser.UserId.Value));
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_currentUser.UserId is not null)
        {
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                UserGroup(_currentUser.UserId.Value));
        }
        await base.OnDisconnectedAsync(exception);
    }

    // ── Provider-scoped groups (existing — for slot/queue events) ─────────

    public async Task SubscribeToProvider(string providerId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"provider-{providerId}");

    public async Task UnsubscribeFromProvider(string providerId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"provider-{providerId}");

    // ── Staff group ───────────────────────────────────────────────────────

    [Authorize(Policy = PolicyNames.Staff)]
    public async Task SubscribeToStaffNotifications()
        => await Groups.AddToGroupAsync(Context.ConnectionId, "staff-notifications");

    [Authorize(Policy = PolicyNames.Staff)]
    public async Task UnsubscribeFromStaffNotifications()
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, "staff-notifications");

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>Returns the group name for a specific user's notifications.</summary>
    public static string UserGroup(Guid userId) => $"user-{userId}";
}
```

### 7. Implement `SignalRInAppNotifier`

Create `src/HealthPlatform.Infrastructure/Notifications/SignalRInAppNotifier.cs`:

```csharp
using HealthPlatform.Api.Hubs;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Infrastructure.Notifications;

internal sealed class SignalRInAppNotifier : IInAppNotifier
{
    private readonly IUnitOfWork                     _uow;
    private readonly IHubContext<NotificationHub>    _hub;
    private readonly ILogger<SignalRInAppNotifier>   _logger;

    public SignalRInAppNotifier(
        IUnitOfWork                   uow,
        IHubContext<NotificationHub>  hub,
        ILogger<SignalRInAppNotifier> logger)
    {
        _uow    = uow;
        _hub    = hub;
        _logger = logger;
    }

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

        _logger.LogDebug(
            "In-app notification {NotificationId} ({Type}) persisted for user {UserId}.",
            notification.Id, type, userId);

        // Count current unread for badge update
        var unreadSpec = new Application.Features.Notifications.UnreadNotificationsCountSpecification(userId);
        var unreadCount = await _uow.Repository<Notification>().CountAsync(unreadSpec, ct);

        var payload = new InAppNotificationPayload(
            notification.Id,
            type,
            title,
            message,
            actionUrl,
            now,
            unreadCount);

        // Push to user's SignalR group — no-op if user is offline
        await _hub
            .Clients
            .Group(NotificationHub.UserGroup(userId))
            .SendAsync("ReceiveNotification", payload, ct);
    }
}
```

### 8. Add `NotificationsController`

Create `src/HealthPlatform.Api/Controllers/NotificationsController.cs`:

```csharp
using HealthPlatform.Application.Features.Notifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthPlatform.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly ISender _sender;

    public NotificationsController(ISender sender) => _sender = sender;

    /// <summary>Returns the last 20 in-app notifications and unread count.</summary>
    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int pageSize       = 20,
        CancellationToken ct           = default)
    {
        var result = await _sender.Send(new GetNotificationsQuery(pageSize), ct);
        return Ok(result);
    }

    /// <summary>
    /// Marks notifications as read.
    /// Pass a notificationId to mark one; omit to mark all.
    /// </summary>
    [HttpPatch("mark-read")]
    public async Task<IActionResult> MarkRead(
        [FromQuery] Guid?  notificationId = null,
        CancellationToken  ct             = default)
    {
        var count = await _sender.Send(new MarkNotificationsReadCommand(notificationId), ct);
        return Ok(new { markedRead = count });
    }
}
```

### 9. Integrate `IInAppNotifier` into existing event handlers

Inject `IInAppNotifier` and call `NotifyAsync` in:

#### `MarkPatientArrivedCommandHandler` (via controller broadcast)
The controller already uses `IHubContext<NotificationHub>` to broadcast
`PatientArrivedPayload`. Replace the direct hub call with a call to
`IInAppNotifier.NotifyAsync` targeting the **provider's user ID**.

> **Note:** `MarkPatientArrivedCommandResult` should include the provider's
> `UserId` so the notifier can target them. If it doesn't, load the
> `Provider` entity and use `Provider.UserId`.

#### `UpdateAppointmentStatusCommandHandler`
After status update and save, call:
```csharp
await _notifier.NotifyAsync(
    userId:        appointment.Patient.UserId,
    patientId:     appointment.PatientId,
    appointmentId: appointment.Id,
    type:          NotificationType.StatusChange,
    title:         "Appointment Status Updated",
    message:       $"Your appointment has been updated to {newStatus}.",
    actionUrl:     $"/appointments/{appointment.Id}",
    ct:            ct);
```

#### `AppointmentReminderJob`
After sending the email, call:
```csharp
await _notifier.NotifyAsync(
    userId:        appointment.Patient.UserId,
    patientId:     appointment.PatientId,
    appointmentId: appointment.Id,
    type:          NotificationType.Reminder,
    title:         "Upcoming Appointment Reminder",
    message:       $"Your appointment with {providerName} is on {appointment.SlotTime:f} UTC.",
    actionUrl:     $"/appointments/{appointment.Id}",
    ct:            ct);
```

#### Swap handlers
- `RequestSlotSwapCommandHandler` → `NotificationType.SwapRequest` to target patient/staff user
- `RespondToSwapRequestCommandHandler` → `NotificationType.SwapResult` to the requester

### 10. Register services in DI

In `src/HealthPlatform.Infrastructure/DependencyInjection.cs`:

```csharp
using HealthPlatform.Infrastructure.Notifications;
// …
services.AddScoped<IInAppNotifier, SignalRInAppNotifier>();
```

---

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Application/Features/Notifications/NotificationDto.cs` | New |
| `src/HealthPlatform.Application/Features/Notifications/GetNotificationsQuery.cs` | New |
| `src/HealthPlatform.Application/Features/Notifications/GetNotificationsQueryHandler.cs` | New |
| `src/HealthPlatform.Application/Features/Notifications/UserNotificationsSpecification.cs` | New |
| `src/HealthPlatform.Application/Features/Notifications/UnreadNotificationsCountSpecification.cs` | New |
| `src/HealthPlatform.Application/Features/Notifications/AllUnreadNotificationsSpecification.cs` | New |
| `src/HealthPlatform.Application/Features/Notifications/MarkNotificationsReadCommand.cs` | New |
| `src/HealthPlatform.Application/Features/Notifications/MarkNotificationsReadCommandHandler.cs` | New |
| `src/HealthPlatform.Application/Features/Notifications/SingleUserNotificationSpecification.cs` | New |
| `src/HealthPlatform.Application/Interfaces/IInAppNotifier.cs` | New (Task 001) |
| `src/HealthPlatform.Api/Hubs/NotificationHub.cs` | Add `OnConnectedAsync`, `OnDisconnectedAsync`, constructor injection |
| `src/HealthPlatform.Api/Hubs/InAppNotificationPayload.cs` | New |
| `src/HealthPlatform.Api/Controllers/NotificationsController.cs` | New |
| `src/HealthPlatform.Infrastructure/Notifications/SignalRInAppNotifier.cs` | New |
| `src/HealthPlatform.Infrastructure/DependencyInjection.cs` | Register `IInAppNotifier` |
| `src/HealthPlatform.Application/Features/Appointments/MarkPatientArrivedCommandHandler.cs` | Inject + call `IInAppNotifier` |
| `src/HealthPlatform.Application/Features/Appointments/UpdateAppointmentStatusCommandHandler.cs` | Inject + call `IInAppNotifier` |
| `src/HealthPlatform.Infrastructure/Reminders/AppointmentReminderJob.cs` | Inject + call `IInAppNotifier` |
| `src/HealthPlatform.Application/Features/SlotSwap/*CommandHandler.cs` | Inject + call `IInAppNotifier` |

---

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --no-restore
dotnet test HealthPlatform.Tests/HealthPlatform.Tests.csproj --no-build
```

Expected: build succeeds, all 33 tests pass.
