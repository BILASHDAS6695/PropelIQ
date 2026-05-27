# Task 003: Unit Tests for Reminder Scheduling

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-033 |
| **Epic** | EP-004 |
| **Layer** | Tests |
| **Priority** | High |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | Task 001 + Task 002 complete; Moq 4.20.72 already in test project (US-032) |

## Objective

Verify the three key behavioural contracts of the reminder scheduling service:

1. **`HangfireReminderScheduler.ScheduleAsync`** skips past-due jobs correctly
   (< 2 h window → both skipped; < 24 h window → 24 h skipped only).
2. **`AppointmentReminderJob.ExecuteAsync`** is idempotent — does not send email
   when the appointment is in a terminal status.
3. **Existing handler tests remain green** — `BookAppointmentCommandTests` and
   any reschedule/cancel tests register a `NoOpReminderScheduler` test double so
   the new constructor parameter is satisfied.

---

## Acceptance Criteria Covered

- AC: Appointment booked < 2 h from now → both reminder jobs skipped
- AC: Appointment booked < 24 h from now → 24 h job skipped, 2 h job scheduled
- AC: Reminder not sent if appointment already Cancelled / Completed / NoShow
- AC: Reminder job idempotent (safe to execute multiple times)

---

## Implementation Steps

### 1. Create `ReminderSchedulerTests.cs`

Create `src/HealthPlatform.Tests/Application/ReminderSchedulerTests.cs`:

```csharp
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using HealthPlatform.Infrastructure.Reminders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace HealthPlatform.Tests.Application;

public sealed class ReminderSchedulerTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static HangfireReminderScheduler BuildScheduler(
        Mock<IBackgroundJobClient> mockClient,
        int hoursBeforeFirst  = 24,
        int hoursBeforeSecond = 2)
    {
        var settings  = Options.Create(new ReminderSettings
        {
            HoursBeforeFirst  = hoursBeforeFirst,
            HoursBeforeSecond = hoursBeforeSecond,
        });
        var uow = new EmptyUnitOfWork();   // no-op UoW — SaveChangesAsync is sufficient
        return new HangfireReminderScheduler(
            mockClient.Object,
            uow,
            settings,
            NullLogger<HangfireReminderScheduler>.Instance);
    }

    private static Appointment MakeAppointment(DateTimeOffset slotTime) =>
        new() { Id = Guid.NewGuid(), SlotTime = slotTime, Status = AppointmentStatus.Scheduled };

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ScheduleAsync_SlotMoreThan24hAway_SchedulesBothJobs()
    {
        // Arrange
        var mockClient = new Mock<IBackgroundJobClient>();
        mockClient
            .Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("job-id");

        var scheduler   = BuildScheduler(mockClient);
        var appointment = MakeAppointment(DateTimeOffset.UtcNow.AddHours(30));

        // Act
        await scheduler.ScheduleAsync(appointment);

        // Assert — Create called twice (once for each reminder)
        mockClient.Verify(
            c => c.Create(
                It.Is<Job>(j => j.Type == typeof(AppointmentReminderJob)),
                It.IsAny<ScheduledState>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ScheduleAsync_SlotLessThan24hAway_SkipsFirstJob()
    {
        // Arrange — slot in 10 h: 24 h reminder would be 14 h in the past
        var mockClient = new Mock<IBackgroundJobClient>();
        mockClient
            .Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("job-id");

        var scheduler   = BuildScheduler(mockClient);
        var appointment = MakeAppointment(DateTimeOffset.UtcNow.AddHours(10));

        // Act
        await scheduler.ScheduleAsync(appointment);

        // Assert — only one job scheduled (the 2 h reminder)
        mockClient.Verify(
            c => c.Create(
                It.Is<Job>(j => j.Type == typeof(AppointmentReminderJob)),
                It.IsAny<ScheduledState>()),
            Times.Once);
        Assert.Null(appointment.Reminder24hJobId);
        Assert.NotNull(appointment.Reminder2hJobId);
    }

    [Fact]
    public async Task ScheduleAsync_SlotLessThan2hAway_SkipsBothJobs()
    {
        // Arrange — slot in 1 h: both trigger times are in the past
        var mockClient  = new Mock<IBackgroundJobClient>();
        var scheduler   = BuildScheduler(mockClient);
        var appointment = MakeAppointment(DateTimeOffset.UtcNow.AddHours(1));

        // Act
        await scheduler.ScheduleAsync(appointment);

        // Assert — no jobs scheduled
        mockClient.Verify(
            c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()),
            Times.Never);
        Assert.Null(appointment.Reminder24hJobId);
        Assert.Null(appointment.Reminder2hJobId);
    }

    [Fact]
    public void Cancel_DeletesPendingJobs_AndNullsJobIds()
    {
        // Arrange
        var mockClient = new Mock<IBackgroundJobClient>();
        mockClient.Setup(c => c.Delete(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var scheduler = BuildScheduler(mockClient);
        var appointment = MakeAppointment(DateTimeOffset.UtcNow.AddHours(30));
        appointment.Reminder24hJobId = "job-24h";
        appointment.Reminder2hJobId  = "job-2h";

        // Act
        scheduler.Cancel(appointment);

        // Assert
        mockClient.Verify(c => c.Delete("job-24h", It.IsAny<string>()), Times.Once);
        mockClient.Verify(c => c.Delete("job-2h",  It.IsAny<string>()), Times.Once);
        Assert.Null(appointment.Reminder24hJobId);
        Assert.Null(appointment.Reminder2hJobId);
    }

    [Fact]
    public void Cancel_NoJobs_DoesNotCallDelete()
    {
        // Arrange — appointment with no scheduled reminders
        var mockClient  = new Mock<IBackgroundJobClient>();
        var scheduler   = BuildScheduler(mockClient);
        var appointment = MakeAppointment(DateTimeOffset.UtcNow.AddHours(30));
        // JobIds are null by default

        // Act
        scheduler.Cancel(appointment);

        // Assert
        mockClient.Verify(c => c.Delete(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}

// ── Test doubles ──────────────────────────────────────────────────────────────

/// <summary>
/// No-op unit of work stub for scheduler tests — only <see cref="SaveChangesAsync"/>
/// matters; repository calls are not expected.
/// </summary>
internal sealed class EmptyUnitOfWork : IUnitOfWork
{
    public IRepository<T> Repository<T>() where T : class => new EmptyRepository<T>();
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    public void Dispose() { }
}

/// <summary>
/// No-op <see cref="IReminderScheduler"/> for use in handler tests that
/// pre-date US-033 so they satisfy the new constructor parameter.
/// </summary>
internal sealed class NoOpReminderScheduler : IReminderScheduler
{
    public Task ScheduleAsync(Appointment appointment, CancellationToken ct = default)
        => Task.CompletedTask;
    public void Cancel(Appointment appointment) { }
}
```

> **Note on `IBackgroundJobClient.Delete`**: Hangfire's interface signature is
> `bool Delete(string jobId, string fromState = null)`.  Use `It.IsAny<string>()`
> for the optional `fromState` parameter in Moq `Verify` calls.

### 2. Create `AppointmentReminderJobTests.cs`

Create `src/HealthPlatform.Tests/Application/AppointmentReminderJobTests.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using HealthPlatform.Infrastructure.Reminders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HealthPlatform.Tests.Application;

public sealed class AppointmentReminderJobTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static Appointment MakeAppointment(AppointmentStatus status)
    {
        var user    = new User { Email = "patient@example.com" };
        var profile = new PatientProfile { FirstName = "Jane", LastName = "Doe", User = user };
        var provider = new Provider { Name = "Dr. Smith" };

        return new Appointment
        {
            Id         = Guid.NewGuid(),
            Status     = status,
            SlotTime   = DateTimeOffset.UtcNow.AddHours(24),
            Patient    = profile,
            Provider   = provider,
            PatientId  = profile.Id,
            ProviderId = provider.Id,
        };
    }

    private static AppointmentReminderJob BuildJob(
        IUnitOfWork  uow,
        IEmailSender emailSender) =>
        new(uow, emailSender, NullLogger<AppointmentReminderJob>.Instance);

    // ── tests ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.NoShow)]
    public async Task ExecuteAsync_TerminalStatus_DoesNotSendEmail(AppointmentStatus terminalStatus)
    {
        // Arrange
        var appointment  = MakeAppointment(terminalStatus);
        var stubUow      = new AppointmentStubUnitOfWork(appointment);
        var mockSender   = new Mock<IEmailSender>();

        var job = BuildJob(stubUow, mockSender.Object);

        // Act
        await job.ExecuteAsync(appointment.Id);

        // Assert — no email sent for terminal states
        mockSender.Verify(
            s => s.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ScheduledAppointment_SendsReminderEmail()
    {
        // Arrange
        var appointment = MakeAppointment(AppointmentStatus.Scheduled);
        var stubUow     = new AppointmentStubUnitOfWork(appointment);
        var mockSender  = new Mock<IEmailSender>();

        var job = BuildJob(stubUow, mockSender.Object);

        // Act
        await job.ExecuteAsync(appointment.Id);

        // Assert — email sent once to patient address
        mockSender.Verify(
            s => s.SendAsync(
                "patient@example.com",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_AppointmentNotFound_DoesNotThrow()
    {
        // Arrange — UoW returns nothing
        var stubUow    = new AppointmentStubUnitOfWork(appointment: null);
        var mockSender = new Mock<IEmailSender>();
        var job        = BuildJob(stubUow, mockSender.Object);

        // Act — should log and return, not throw
        var ex = await Record.ExceptionAsync(() => job.ExecuteAsync(Guid.NewGuid()));

        // Assert
        Assert.Null(ex);
        mockSender.Verify(
            s => s.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

// ── Test doubles ──────────────────────────────────────────────────────────────

/// <summary>
/// Returns a single <see cref="Appointment"/> (or nothing) for any
/// <see cref="IRepository{Appointment}.GetAsync"/> call.
/// </summary>
internal sealed class AppointmentStubUnitOfWork : IUnitOfWork
{
    private readonly Appointment? _appointment;

    public AppointmentStubUnitOfWork(Appointment? appointment)
        => _appointment = appointment;

    public IRepository<T> Repository<T>() where T : class
    {
        if (typeof(T) == typeof(Appointment))
            return (IRepository<T>)(object)new AppointmentStubRepo(_appointment);

        return new EmptyRepository<T>();
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    public void Dispose() { }
}

internal sealed class AppointmentStubRepo : IRepository<Appointment>
{
    private readonly Appointment? _appointment;

    public AppointmentStubRepo(Appointment? appointment) => _appointment = appointment;

    public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_appointment?.Id == id ? _appointment : null);

    public Task<IReadOnlyList<Appointment>> GetAsync(
        ISpecification<Appointment> spec, CancellationToken ct = default)
    {
        IReadOnlyList<Appointment> result = _appointment is null
            ? Array.Empty<Appointment>()
            : [_appointment];
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Appointment>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Appointment>>(Array.Empty<Appointment>());

    public Task<int> CountAsync(ISpecification<Appointment> spec, CancellationToken ct = default)
        => Task.FromResult(_appointment is null ? 0 : 1);

    public Task AddAsync(Appointment entity, CancellationToken ct = default) => Task.CompletedTask;
    public void Update(Appointment entity) { }
    public void Delete(Appointment entity) { }
}
```

### 3. Update `BookAppointmentCommandTests.cs` — add `NoOpReminderScheduler`

In the existing `BuildSender` helper, add the `IReminderScheduler` registration:

```csharp
private static ISender BuildSender(
    IUnitOfWork         uow,
    ICurrentUserService currentUser,
    IEmailSender        emailSender)
{
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddApplication();
    services.AddScoped(_ => uow);
    services.AddScoped(_ => currentUser);
    services.AddScoped(_ => emailSender);
    services.AddScoped<IReminderScheduler>(_ => new NoOpReminderScheduler());  // ← add
    return services.BuildServiceProvider().GetRequiredService<ISender>();
}
```

> Add similar `NoOpReminderScheduler` registration to any other existing handler
> test helpers (reschedule, cancel) that build a mediator with `AddApplication()`.

---

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Tests/Application/ReminderSchedulerTests.cs` | New — 5 scheduler tests |
| `src/HealthPlatform.Tests/Application/AppointmentReminderJobTests.cs` | New — 5 job tests (3 terminal + 1 active + 1 not-found) |
| `src/HealthPlatform.Tests/Application/BookAppointmentCommandTests.cs` | Add `NoOpReminderScheduler` to `BuildSender` |
| Other handler test files (cancel, reschedule, walk-in, etc.) | Add `NoOpReminderScheduler` where `AddApplication()` is used |

---

## Verification

```bash
dotnet test src/HealthPlatform.sln --configuration Release \
  --logger "console;verbosity=normal"
# Expect: all pre-existing tests pass + 10 new tests (5 scheduler + 5 job)
# Total should be 33 (23 existing + 10 new)
```

---

## Notes

- `EmptyUnitOfWork` is defined in `ReminderSchedulerTests.cs` (same assembly).
  If it conflicts with another definition added elsewhere, extract to a shared
  `TestHelpers.cs` file.
- The `AppointmentReminderJob` tests require `InternalsVisibleTo` on the
  Infrastructure project — already added in US-032 Task 003.
- `ScheduledState` (Hangfire) is the state class for `IBackgroundJobClient.Schedule`.
  Import with `using Hangfire.States;`.
