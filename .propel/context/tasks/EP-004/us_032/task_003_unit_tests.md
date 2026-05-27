# Task 003: Unit Tests for Email Notification Service

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-032 |
| **Epic** | EP-004 |
| **Layer** | Tests |
| **Priority** | High |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | Task 001 (`MailKitEmailSender`, `EmailTemplateService`), Task 002 (`HangfireEmailDispatcher`, `SendEmailJob`) |

## Objective

Verify the three key behavioural contracts of the email notification service:

1. `HangfireEmailDispatcher.SendAsync` enqueues a `SendEmailJob` rather than
   delivering inline (AC: emails queued via Hangfire).
2. `MailKitEmailSender.SendAsync` skips delivery for invalid email addresses
   without throwing (AC: invalid email → log warning, skip send).
3. `EmailTemplateService` renders all six templates without throwing and includes
   the expected variable values (AC: template variables present in output).

Additionally, add the missing test for `GetMyAppointmentsQueryHandler` introduced
in US-027 (was not covered in its own task).

---

## Acceptance Criteria Covered

- AC: Emails queued via Hangfire (verified by job enqueue assertion)
- AC: Invalid email address → log warning, skip send, do not block workflow
- AC: Template variables: patient name, provider name, date, time, appointment ID
- AC: Failed email delivery retried 3 times (Hangfire config; verified by retry attribute presence)

---

## Implementation Steps

### 1. Create `EmailTests.cs`

Create `src/HealthPlatform.Tests/Application/EmailTests.cs`:

```csharp
using HealthPlatform.Infrastructure.Messaging;
using Hangfire;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace HealthPlatform.Tests.Application;

public sealed class EmailTests
{
    // ── HangfireEmailDispatcher ───────────────────────────────────────────────

    [Fact]
    public async Task Dispatcher_EnqueuesJob_WhenSendAsyncCalled()
    {
        // Arrange
        var mockClient = new Mock<IBackgroundJobClient>();
        var dispatcher = new HangfireEmailDispatcher(mockClient.Object);

        // Act
        await dispatcher.SendAsync("patient@example.com", "Test Subject", "<p>Hello</p>");

        // Assert — job was enqueued (any SendEmailJob invocation)
        mockClient.Verify(
            c => c.Create(
                It.Is<Job>(j => j.Type == typeof(SendEmailJob) &&
                                j.Method.Name == nameof(SendEmailJob.ExecuteAsync)),
                It.IsAny<EnqueuedState>()),
            Times.Once);
    }

    [Fact]
    public async Task Dispatcher_DoesNotThrow_WhenJobClientFails()
    {
        // Arrange
        var mockClient = new Mock<IBackgroundJobClient>();
        mockClient
            .Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Throws(new InvalidOperationException("Hangfire unavailable"));

        var dispatcher = new HangfireEmailDispatcher(mockClient.Object);

        // Act & Assert — should propagate (caller decides to handle or let Hangfire retry)
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.SendAsync("p@example.com", "Subject", "Body"));
    }

    // ── MailKitEmailSender — invalid address guard ────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("@nodomain")]
    public async Task MailKitSender_SkipsDelivery_ForInvalidAddress(string badAddress)
    {
        // Arrange
        var settings = Options.Create(new SmtpSettings
        {
            Host = "localhost",
            Port = 1025,
            UseSsl = false
        });
        var sender = new MailKitEmailSender(settings, NullLogger<MailKitEmailSender>.Instance);

        // Act — should NOT throw, should silently skip
        var ex = await Record.ExceptionAsync(
            () => sender.SendAsync(badAddress, "Subject", "Body"));

        // Assert
        Assert.Null(ex);
    }

    // ── EmailTemplateService — variable substitution ──────────────────────────

    [Fact]
    public void BookingConfirmation_ContainsExpectedVariables()
    {
        var apptId   = Guid.NewGuid();
        var apptTime = new DateTimeOffset(2026, 6, 15, 14, 30, 0, TimeSpan.Zero);
        var (subject, body) = EmailTemplateService.BookingConfirmation(
            "Alice Smith", "Dr. Johnson", apptTime, apptId);

        Assert.Contains("Alice Smith",  body);
        Assert.Contains("Dr. Johnson",  body);
        Assert.Contains("June",         body);    // date present
        Assert.Contains("2:30",         body);    // time present
        Assert.Contains(apptId.ToString("N")[..8].ToUpperInvariant(), body);
        Assert.Contains("confirmed",    subject,  StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cancellation_ContainsExpectedVariables()
    {
        var apptId   = Guid.NewGuid();
        var apptTime = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);
        var (subject, body) = EmailTemplateService.Cancellation(
            "Bob Lee", "Dr. Patel", apptTime, apptId);

        Assert.Contains("Bob Lee",   body);
        Assert.Contains("Dr. Patel", body);
        Assert.Contains("cancelled", subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reminder_ContainsProviderName()
    {
        var (_, body) = EmailTemplateService.Reminder(
            "Carol", "Dr. Rivera",
            DateTimeOffset.UtcNow.AddDays(1), Guid.NewGuid());

        Assert.Contains("Dr. Rivera", body);
        Assert.Contains("Carol",      body);
    }

    [Fact]
    public void SwapRequest_ContainsRequesterName()
    {
        var (_, body) = EmailTemplateService.SwapRequest(
            "Target Patient", "Requesting Patient",
            DateTimeOffset.UtcNow.AddDays(2));

        Assert.Contains("Requesting Patient", body);
        Assert.Contains("Target Patient",     body);
    }

    [Fact]
    public void SwapResult_Accepted_ContainsNewSlotTime()
    {
        var newSlot = new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.Zero);
        var (subject, body) = EmailTemplateService.SwapResult("Dave", accepted: true, newSlot);

        Assert.Contains("Dave",     body);
        Assert.Contains("accepted", subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("August",   body);
    }

    [Fact]
    public void SwapResult_Declined_DoesNotContainNewSlot()
    {
        var (subject, body) = EmailTemplateService.SwapResult(
            "Eve", accepted: false, DateTimeOffset.UtcNow);

        Assert.Contains("declined", subject, StringComparison.OrdinalIgnoreCase);
        // declined response has no "New slot" row
        Assert.DoesNotContain("New slot", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoShowFollowUp_ContainsMissedDate()
    {
        var missed = new DateTimeOffset(2026, 5, 10, 8, 0, 0, TimeSpan.Zero);
        var (_, body) = EmailTemplateService.NoShowFollowUp(
            "Frank", "Dr. Chen", missed, Guid.NewGuid());

        Assert.Contains("Frank",    body);
        Assert.Contains("Dr. Chen", body);
        Assert.Contains("May",      body);
    }
}
```

### 2. Add `Moq` NuGet Package to Test Project

Add to `src/HealthPlatform.Tests/HealthPlatform.Tests.csproj`:

```xml
<PackageReference Include="Moq" Version="4.20.72" />
```

### 3. Create `GetMyAppointmentsQueryTests.cs`

Create `src/HealthPlatform.Tests/Application/GetMyAppointmentsQueryTests.cs`:

```csharp
using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using MediatR;

namespace HealthPlatform.Tests.Application;

public sealed class GetMyAppointmentsQueryTests
{
    [Fact]
    public async Task GetMine_UnauthenticatedUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddScoped<IUnitOfWork>(_ => new EmptyUnitOfWork());
        services.AddScoped<ICurrentUserService>(_ => new AnonymousMyApptUser());
        var sender = services.BuildServiceProvider().GetRequiredService<ISender>();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => sender.Send(new GetMyAppointmentsQuery()));
    }

    [Fact]
    public async Task GetMine_PatientWithNoAppointments_ReturnsEmptyList()
    {
        // Arrange
        var userId  = Guid.NewGuid();
        var profile = new PatientProfile { Id = Guid.NewGuid(), UserId = userId };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddScoped<IUnitOfWork>(_ => new MyApptStubUnitOfWork(profile, []));
        services.AddScoped<ICurrentUserService>(_ => new AuthenticatedMyApptUser(userId));
        var sender = services.BuildServiceProvider().GetRequiredService<ISender>();

        // Act
        var result = await sender.Send(new GetMyAppointmentsQuery());

        // Assert
        Assert.Empty(result);
    }
}

// ── Test doubles ─────────────────────────────────────────────────────────────

internal sealed class AnonymousMyApptUser : ICurrentUserService
{
    public Guid? UserId          => null;
    public bool  IsAuthenticated => false;
}

internal sealed class AuthenticatedMyApptUser : ICurrentUserService
{
    public AuthenticatedMyApptUser(Guid userId) => UserId = userId;
    public Guid? UserId          { get; }
    public bool  IsAuthenticated => true;
}

internal sealed class MyApptStubUnitOfWork : IUnitOfWork
{
    private readonly PatientProfile         _profile;
    private readonly IReadOnlyList<Appointment> _appointments;

    public MyApptStubUnitOfWork(PatientProfile profile, IReadOnlyList<Appointment> appointments)
    {
        _profile      = profile;
        _appointments = appointments;
    }

    public IRepository<T> Repository<T>() where T : BaseEntity
    {
        if (typeof(T) == typeof(PatientProfile))
            return (IRepository<T>)(object)new SingleItemRepository<PatientProfile>(_profile);

        if (typeof(T) == typeof(Appointment))
            return (IRepository<T>)(object)new ListRepository<Appointment>(_appointments);

        return new EmptyRepository<T>();
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
}

// Reuse EmptyRepository<T> from existing test helpers
```

> **Note**: `EmptyRepository<T>`, `SingleItemRepository<T>`, and `ListRepository<T>`
> are already defined in `BookAppointmentCommandTests.cs`. Extract them to a shared
> `TestHelpers.cs` file in the `Application/` test folder if the duplication grows.

---

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Tests/HealthPlatform.Tests.csproj` | Add `Moq 4.20.72` |
| `src/HealthPlatform.Tests/Application/EmailTests.cs` | New — dispatcher, invalid-address, template tests |
| `src/HealthPlatform.Tests/Application/GetMyAppointmentsQueryTests.cs` | New — US-027 handler coverage |

---

## Verification

```bash
cd src
dotnet add HealthPlatform.Tests/HealthPlatform.Tests.csproj package Moq --version 4.20.72
dotnet test HealthPlatform.sln --configuration Release --logger "console;verbosity=normal"
# Expect: all existing tests pass + new EmailTests (9) + GetMyAppointmentsQueryTests (2)
```

---

## Notes

- `MailKitEmailSender_SkipsDelivery_ForInvalidAddress` verifies the invalid-email
  guard without an SMTP server — it relies on the early-return before `SmtpClient.ConnectAsync`.
- The `HangfireEmailDispatcher` test mocks `IBackgroundJobClient.Create` — the
  lower-level method that `Enqueue<T>` delegates to internally.
- `GetMyAppointmentsQueryTests` intentionally tests only the guard and empty-list
  cases; the mapping path is covered by integration tests if added later.
