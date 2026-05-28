# Task 002: CQRS Handlers, Preference Checker, API Controller & Notification Guard Integration

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-035 |
| **Epic** | EP-004 |
| **Layer** | Infrastructure + Application + API + Tests |
| **Priority** | Low |
| **Estimated Effort** | 50 minutes |
| **Dependencies** | Task 001 complete — `NotificationPreferences`, `INotificationPreferenceChecker`, migration applied |

## Objective

1. **Implement `NotificationPreferenceCheckerService`** (Infrastructure) — loads
   the user's `NotificationPreferences` from the DB and returns whether a given
   channel + type combination is allowed.
2. **Add CQRS** — `GetNotificationPreferencesQuery` and
   `UpdateNotificationPreferencesCommand` with validator and handler.
3. **Add `NotificationPreferencesController`** — `GET` and `PUT` endpoints at
   `/api/users/{id}/notification-preferences`.
4. **Integrate preference guard** — inject `INotificationPreferenceChecker` into
   `AppointmentReminderJob`, `InitiateSwapRequestCommandHandler`,
   `RespondToSwapRequestCommandHandler`, and `SignalRInAppNotifier` so
   notifications are suppressed when the user has opted out.
5. **Register** new services in DI.
6. **Add unit tests** for the new handlers and the preference checker.

---

## Acceptance Criteria Covered

- AC: Notification service checks preferences before sending
- AC: Critical notifications (security: lockout, password expiry) cannot be disabled
- AC: Preferences API: `GET/PUT /users/{id}/notification-preferences`
- AC: Changes take effect immediately (no restart needed)
- AC: User disables all email notifications → in-app still active
- AC: User disables all notifications → security notifications still delivered

---

## Implementation Steps

### 1. Add `NotificationPreferencesDto`

Create `src/HealthPlatform.Application/Features/NotificationPreferences/NotificationPreferencesDto.cs`:

```csharp
namespace HealthPlatform.Application.Features.NotificationPreferences;

public sealed record NotificationPreferencesDto(
    bool EmailReminders,
    bool EmailSwap,
    bool EmailGeneral,
    bool InAppReminders,
    bool InAppSwap,
    bool InAppGeneral);
```

---

### 2. Add `GetNotificationPreferencesQuery`

Create `src/HealthPlatform.Application/Features/NotificationPreferences/GetNotificationPreferencesQuery.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.NotificationPreferences;

public sealed record GetNotificationPreferencesQuery(Guid UserId)
    : IRequest<NotificationPreferencesDto>;
```

---

### 3. Add `GetNotificationPreferencesQueryHandler`

Create `src/HealthPlatform.Application/Features/NotificationPreferences/GetNotificationPreferencesQueryHandler.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.NotificationPreferences;

internal sealed class GetNotificationPreferencesQueryHandler
    : IRequestHandler<GetNotificationPreferencesQuery, NotificationPreferencesDto>
{
    private readonly IUnitOfWork _uow;

    public GetNotificationPreferencesQueryHandler(IUnitOfWork uow)
        => _uow = uow;

    public async Task<NotificationPreferencesDto> Handle(
        GetNotificationPreferencesQuery request,
        CancellationToken ct)
    {
        var user = await _uow.Repository<User>().GetByIdAsync(request.UserId, ct)
                   ?? throw new KeyNotFoundException($"User {request.UserId} not found.");

        var p = user.NotificationPreferences;
        return new NotificationPreferencesDto(
            p.EmailReminders,
            p.EmailSwap,
            p.EmailGeneral,
            p.InAppReminders,
            p.InAppSwap,
            p.InAppGeneral);
    }
}
```

---

### 4. Add `UpdateNotificationPreferencesCommand`

Create `src/HealthPlatform.Application/Features/NotificationPreferences/UpdateNotificationPreferencesCommand.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.NotificationPreferences;

public sealed record UpdateNotificationPreferencesCommand(
    Guid UserId,
    bool EmailReminders,
    bool EmailSwap,
    bool EmailGeneral,
    bool InAppReminders,
    bool InAppSwap,
    bool InAppGeneral) : IRequest;
```

---

### 5. Add `UpdateNotificationPreferencesCommandValidator`

Create `src/HealthPlatform.Application/Features/NotificationPreferences/UpdateNotificationPreferencesCommandValidator.cs`:

```csharp
using FluentValidation;

namespace HealthPlatform.Application.Features.NotificationPreferences;

internal sealed class UpdateNotificationPreferencesCommandValidator
    : AbstractValidator<UpdateNotificationPreferencesCommand>
{
    public UpdateNotificationPreferencesCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
    }
}
```

---

### 6. Add `UpdateNotificationPreferencesCommandHandler`

Create `src/HealthPlatform.Application/Features/NotificationPreferences/UpdateNotificationPreferencesCommandHandler.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.NotificationPreferences;

internal sealed class UpdateNotificationPreferencesCommandHandler
    : IRequestHandler<UpdateNotificationPreferencesCommand>
{
    private readonly IUnitOfWork _uow;

    public UpdateNotificationPreferencesCommandHandler(IUnitOfWork uow)
        => _uow = uow;

    public async Task Handle(
        UpdateNotificationPreferencesCommand request,
        CancellationToken ct)
    {
        var user = await _uow.Repository<User>().GetByIdAsync(request.UserId, ct)
                   ?? throw new KeyNotFoundException($"User {request.UserId} not found.");

        user.NotificationPreferences.EmailReminders = request.EmailReminders;
        user.NotificationPreferences.EmailSwap      = request.EmailSwap;
        user.NotificationPreferences.EmailGeneral   = request.EmailGeneral;
        user.NotificationPreferences.InAppReminders = request.InAppReminders;
        user.NotificationPreferences.InAppSwap      = request.InAppSwap;
        user.NotificationPreferences.InAppGeneral   = request.InAppGeneral;

        _uow.Repository<User>().Update(user);
        await _uow.SaveChangesAsync(ct);
    }
}
```

---

### 7. Implement `NotificationPreferenceCheckerService`

Create `src/HealthPlatform.Infrastructure/Notifications/NotificationPreferenceCheckerService.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using HealthPlatform.Domain.ValueObjects;

namespace HealthPlatform.Infrastructure.Notifications;

/// <summary>
/// Loads the user's <see cref="NotificationPreferences"/> from the database
/// and answers whether a given channel + notification type is permitted.
/// Defaults to <c>true</c> (allowed) when the user record cannot be loaded.
/// </summary>
internal sealed class NotificationPreferenceCheckerService : INotificationPreferenceChecker
{
    private readonly IUnitOfWork _uow;

    public NotificationPreferenceCheckerService(IUnitOfWork uow)
        => _uow = uow;

    public async Task<bool> IsAllowedAsync(
        Guid                userId,
        NotificationChannel channel,
        NotificationType    type,
        CancellationToken   ct = default)
    {
        var user = await _uow.Repository<User>().GetByIdAsync(userId, ct);
        if (user is null)
            return true; // default-open: do not silently drop notifications for unknown users

        var prefs = user.NotificationPreferences;
        return channel switch
        {
            NotificationChannel.Email  => IsEmailAllowed(prefs, type),
            NotificationChannel.InApp  => IsInAppAllowed(prefs, type),
            _                          => true, // Sms — not gated by user prefs yet
        };
    }

    private static bool IsEmailAllowed(NotificationPreferences p, NotificationType t) =>
        t switch
        {
            NotificationType.Reminder                  => p.EmailReminders,
            NotificationType.SwapRequest
                or NotificationType.SwapResult
                or NotificationType.SlotSwap           => p.EmailSwap,
            _                                          => p.EmailGeneral,
        };

    private static bool IsInAppAllowed(NotificationPreferences p, NotificationType t) =>
        t switch
        {
            NotificationType.Reminder                  => p.InAppReminders,
            NotificationType.SwapRequest
                or NotificationType.SwapResult
                or NotificationType.SlotSwap           => p.InAppSwap,
            _                                          => p.InAppGeneral,
        };
}
```

---

### 8. Integrate preference check into `SignalRInAppNotifier`

File: `src/HealthPlatform.Api/Notifications/SignalRInAppNotifier.cs`

Add `INotificationPreferenceChecker _prefChecker` to the constructor alongside
the existing `IHubContext<NotificationHub>` and `IUnitOfWork`.

At the top of `NotifyAsync`, before persisting the `Notification` entity, add:

```csharp
if (!await _prefChecker.IsAllowedAsync(userId, NotificationChannel.InApp, type, ct))
    return;
```

Full constructor signature after the change:

```csharp
public SignalRInAppNotifier(
    IHubContext<NotificationHub>        hub,
    IUnitOfWork                         uow,
    INotificationPreferenceChecker      prefChecker)
{
    _hub        = hub;
    _uow        = uow;
    _prefChecker = prefChecker;
}
```

---

### 9. Integrate preference check into `AppointmentReminderJob`

File: `src/HealthPlatform.Infrastructure/Reminders/AppointmentReminderJob.cs`

Add `INotificationPreferenceChecker _prefChecker` to the constructor alongside
existing parameters.

In `ExecuteAsync`, before `_emailSender.SendAsync(...)`:

```csharp
if (await _prefChecker.IsAllowedAsync(appointment.Patient.UserId,
        NotificationChannel.Email, NotificationType.Reminder, ct))
{
    await _emailSender.SendAsync(email, subject, body, ct);
}
```

The `_inAppNotifier.NotifyAsync(...)` call is left as-is — `SignalRInAppNotifier`
now guards itself (Step 8).

---

### 10. Integrate preference check into `InitiateSwapRequestCommandHandler`

File: `src/HealthPlatform.Application/Features/SlotSwap/InitiateSwapRequestCommandHandler.cs`

Add `INotificationPreferenceChecker _prefChecker` to the constructor.

Before the `_email.SendAsync(...)` call for the target patient's swap-request
email, add:

```csharp
if (await _prefChecker.IsAllowedAsync(targetPatientProfile.UserId,
        NotificationChannel.Email, NotificationType.SwapRequest, ct))
{
    await _email.SendAsync(/* existing args */);
}
```

---

### 11. Integrate preference check into `RespondToSwapRequestCommandHandler`

File: `src/HealthPlatform.Application/Features/SlotSwap/RespondToSwapRequestCommandHandler.cs`

Add `INotificationPreferenceChecker _prefChecker` to the constructor.

Before each `_email.SendAsync(...)` call (accept branch: requester + target;
decline branch: requester), wrap with:

```csharp
if (await _prefChecker.IsAllowedAsync(<recipientUserId>,
        NotificationChannel.Email, NotificationType.SwapResult, ct))
{
    await _email.SendAsync(/* existing args */);
}
```

---

### 12. Add `NotificationPreferencesController`

Create `src/HealthPlatform.Api/Controllers/NotificationPreferencesController.cs`:

```csharp
using HealthPlatform.Application.Features.NotificationPreferences;
using HealthPlatform.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthPlatform.Api.Controllers;

[ApiController]
[Route("api/users/{userId:guid}/notification-preferences")]
[Authorize]
public sealed class NotificationPreferencesController : ControllerBase
{
    private readonly IMediator             _mediator;
    private readonly ICurrentUserService   _currentUser;

    public NotificationPreferencesController(
        IMediator           mediator,
        ICurrentUserService currentUser)
    {
        _mediator    = mediator;
        _currentUser = currentUser;
    }

    /// <summary>Returns the notification preferences for the specified user.</summary>
    [HttpGet]
    public async Task<ActionResult<NotificationPreferencesDto>> Get(
        Guid userId,
        CancellationToken ct)
    {
        if (_currentUser.UserId != userId)
            return Forbid();

        var result = await _mediator.Send(new GetNotificationPreferencesQuery(userId), ct);
        return Ok(result);
    }

    /// <summary>Replaces the notification preferences for the specified user.</summary>
    [HttpPut]
    public async Task<IActionResult> Put(
        Guid userId,
        [FromBody] NotificationPreferencesDto body,
        CancellationToken ct)
    {
        if (_currentUser.UserId != userId)
            return Forbid();

        await _mediator.Send(new UpdateNotificationPreferencesCommand(
            userId,
            body.EmailReminders,
            body.EmailSwap,
            body.EmailGeneral,
            body.InAppReminders,
            body.InAppSwap,
            body.InAppGeneral), ct);

        return NoContent();
    }
}
```

---

### 13. Register `NotificationPreferenceCheckerService` in DI

File: `src/HealthPlatform.Infrastructure/DependencyInjection.cs`

Add inside the `AddInfrastructure` extension method:

```csharp
services.AddScoped<INotificationPreferenceChecker, NotificationPreferenceCheckerService>();
```

Add the using:

```csharp
using HealthPlatform.Infrastructure.Notifications;
```

---

### 14. Add unit tests

File: `src/HealthPlatform.Tests/Application/NotificationPreferencesTests.cs`

```csharp
using HealthPlatform.Application.Features.NotificationPreferences;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using HealthPlatform.Domain.ValueObjects;
using HealthPlatform.Infrastructure.Notifications;
using Moq;

namespace HealthPlatform.Tests.Application;

public sealed class NotificationPreferencesTests
{
    // ── GetNotificationPreferencesQueryHandler ────────────────────────────

    [Fact]
    public async Task Get_ReturnsCurrentPreferences()
    {
        var userId = Guid.NewGuid();
        var user   = new User
        {
            Id = userId,
            NotificationPreferences = new NotificationPreferences
            {
                EmailReminders  = false,
                EmailSwap       = true,
                EmailGeneral    = true,
                InAppReminders  = true,
                InAppSwap       = false,
                InAppGeneral    = true,
            },
        };

        var mockRepo = new Mock<IRepository<User>>();
        mockRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Repository<User>()).Returns(mockRepo.Object);

        var handler = new GetNotificationPreferencesQueryHandler(mockUow.Object);
        var result  = await handler.Handle(
            new GetNotificationPreferencesQuery(userId), CancellationToken.None);

        Assert.False(result.EmailReminders);
        Assert.True(result.EmailSwap);
        Assert.False(result.InAppSwap);
    }

    // ── UpdateNotificationPreferencesCommandHandler ───────────────────────

    [Fact]
    public async Task Update_PersistsNewFlags()
    {
        var userId = Guid.NewGuid();
        var user   = new User { Id = userId };

        var mockRepo = new Mock<IRepository<User>>();
        mockRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Repository<User>()).Returns(mockRepo.Object);

        var handler = new UpdateNotificationPreferencesCommandHandler(mockUow.Object);
        await handler.Handle(
            new UpdateNotificationPreferencesCommand(
                userId,
                EmailReminders:  false,
                EmailSwap:       true,
                EmailGeneral:    true,
                InAppReminders:  true,
                InAppSwap:       false,
                InAppGeneral:    true),
            CancellationToken.None);

        Assert.False(user.NotificationPreferences.EmailReminders);
        Assert.False(user.NotificationPreferences.InAppSwap);
        mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── NotificationPreferenceCheckerService ──────────────────────────────

    [Theory]
    [InlineData(NotificationChannel.Email, NotificationType.Reminder,    false, true,  true,  true,  true,  true,  false)]
    [InlineData(NotificationChannel.Email, NotificationType.SwapRequest, true,  false, true,  true,  true,  true,  false)]
    [InlineData(NotificationChannel.InApp, NotificationType.Reminder,    true,  true,  true,  false, true,  true,  false)]
    [InlineData(NotificationChannel.InApp, NotificationType.SwapResult,  true,  true,  true,  true,  false, true,  false)]
    [InlineData(NotificationChannel.Email, NotificationType.StatusChange, true, true,  false, true,  true,  true,  false)]
    [InlineData(NotificationChannel.InApp, NotificationType.General,     true,  true,  true,  true,  true,  false, false)]
    [InlineData(NotificationChannel.Email, NotificationType.Reminder,    true,  true,  true,  true,  true,  true,  true)]
    public async Task Checker_ReturnsExpected(
        NotificationChannel channel,
        NotificationType    type,
        bool emailRem, bool emailSwap, bool emailGen,
        bool inAppRem, bool inAppSwap, bool inAppGen,
        bool expected)
    {
        var userId = Guid.NewGuid();
        var user   = new User
        {
            Id = userId,
            NotificationPreferences = new NotificationPreferences
            {
                EmailReminders  = emailRem,
                EmailSwap       = emailSwap,
                EmailGeneral    = emailGen,
                InAppReminders  = inAppRem,
                InAppSwap       = inAppSwap,
                InAppGeneral    = inAppGen,
            },
        };

        var mockRepo = new Mock<IRepository<User>>();
        mockRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Repository<User>()).Returns(mockRepo.Object);

        var checker = new NotificationPreferenceCheckerService(mockUow.Object);
        var result  = await checker.IsAllowedAsync(userId, channel, type);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task Checker_DefaultsToAllowed_WhenUserNotFound()
    {
        var mockRepo = new Mock<IRepository<User>>();
        mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((User?)null);

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Repository<User>()).Returns(mockRepo.Object);

        var checker = new NotificationPreferenceCheckerService(mockUow.Object);
        var result  = await checker.IsAllowedAsync(
            Guid.NewGuid(), NotificationChannel.Email, NotificationType.Reminder);

        Assert.True(result);
    }
}
```

---

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Release
# Expect: Build succeeded. 0 Error(s)

dotnet test HealthPlatform.Tests/HealthPlatform.Tests.csproj
# Expect: Passed! — all tests pass (new count: 33 + new tests)
```

---

## Notes

- `INotificationPreferenceChecker` is injected into Infrastructure jobs
  (`AppointmentReminderJob`) even though the interface lives in Application —
  this is valid; Infrastructure references Application.
- `SignalRInAppNotifier` lives in the API project, which references
  Infrastructure. Injecting `INotificationPreferenceChecker` (an Application
  interface implemented in Infrastructure) is registered via the API's DI
  container — no circular dependency.
- The `PUT` endpoint returns `204 No Content` (not `200 OK`) to match REST
  conventions for a full replacement operation.
- `ICurrentUserService.UserId` is `Guid?` — the `Forbid()` guard handles both
  unauthenticated access and attempts to modify another user's preferences.
- Security emails (lockout, credential expiry) are sent via `IEmailSender`
  directly from their handlers without calling `INotificationPreferenceChecker`,
  so they are never gated by user prefs — satisfying the "critical notifications
  cannot be disabled" AC.
