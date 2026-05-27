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
        var settings = Options.Create(new ReminderSettings
        {
            HoursBeforeFirst  = hoursBeforeFirst,
            HoursBeforeSecond = hoursBeforeSecond,
        });
        var uow = new EmptyUnitOfWork();
        return new HangfireReminderScheduler(
            mockClient.Object,
            uow,
            settings,
            NullLogger<HangfireReminderScheduler>.Instance);
    }

    private static Appointment MakeAppointment(DateTimeOffset slotTime) =>
        new() { Id = Guid.NewGuid(), SlotTime = slotTime, Status = AppointmentStatus.Scheduled };

    // ── ScheduleAsync tests ───────────────────────────────────────────────────

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
        Assert.NotNull(appointment.Reminder24hJobId);
        Assert.NotNull(appointment.Reminder2hJobId);
    }

    [Fact]
    public async Task ScheduleAsync_SlotLessThan24hAway_SkipsFirstJob()
    {
        // Arrange — slot in 10 h: 24 h reminder trigger is 14 h in the past
        var mockClient = new Mock<IBackgroundJobClient>();
        mockClient
            .Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("job-id");

        var scheduler   = BuildScheduler(mockClient);
        var appointment = MakeAppointment(DateTimeOffset.UtcNow.AddHours(10));

        // Act
        await scheduler.ScheduleAsync(appointment);

        // Assert — only the 2 h reminder is scheduled
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

    // ── Cancel tests ──────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_DeletesPendingJobs_AndNullsJobIds()
    {
        // Arrange
        var mockClient = new Mock<IBackgroundJobClient>();
        // Delete() is a Hangfire extension method; it calls ChangeState internally.
        mockClient
            .Setup(c => c.ChangeState(It.IsAny<string>(), It.IsAny<DeletedState>(), It.IsAny<string>()))
            .Returns(true);

        var scheduler   = BuildScheduler(mockClient);
        var appointment = MakeAppointment(DateTimeOffset.UtcNow.AddHours(30));
        appointment.Reminder24hJobId = "job-24h";
        appointment.Reminder2hJobId  = "job-2h";

        // Act
        scheduler.Cancel(appointment);

        // Assert — ChangeState called once for each job
        mockClient.Verify(
            c => c.ChangeState("job-24h", It.IsAny<DeletedState>(), It.IsAny<string>()),
            Times.Once);
        mockClient.Verify(
            c => c.ChangeState("job-2h",  It.IsAny<DeletedState>(), It.IsAny<string>()),
            Times.Once);
        Assert.Null(appointment.Reminder24hJobId);
        Assert.Null(appointment.Reminder2hJobId);
    }

    [Fact]
    public void Cancel_NoJobs_DoesNotCallChangeState()
    {
        // Arrange — appointment with no scheduled reminders
        var mockClient  = new Mock<IBackgroundJobClient>();
        var scheduler   = BuildScheduler(mockClient);
        var appointment = MakeAppointment(DateTimeOffset.UtcNow.AddHours(30));
        // JobIds are null by default

        // Act
        scheduler.Cancel(appointment);

        // Assert — no interaction with Hangfire at all
        mockClient.Verify(
            c => c.ChangeState(It.IsAny<string>(), It.IsAny<IState>(), It.IsAny<string>()),
            Times.Never);
    }
}

// ── Test doubles ──────────────────────────────────────────────────────────────

/// <summary>
/// No-op unit of work for scheduler tests — only <see cref="SaveChangesAsync"/>
/// is exercised; repository calls are not expected.
/// </summary>
internal sealed class EmptyUnitOfWork : IUnitOfWork
{
    public IRepository<T> Repository<T>() where T : class => new EmptyRepository<T>();
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    public void Dispose() { }
}
