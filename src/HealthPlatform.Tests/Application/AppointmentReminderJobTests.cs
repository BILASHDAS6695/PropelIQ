using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using HealthPlatform.Infrastructure.Reminders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace HealthPlatform.Tests.Application;

public sealed class AppointmentReminderJobTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static Appointment MakeAppointment(AppointmentStatus status)
    {
        var user     = new User { Email = "patient@example.com" };
        var patient  = new PatientProfile { FirstName = "Jane", LastName = "Doe", User = user };
        var provider = new Provider { Name = "Dr. Smith" };

        return new Appointment
        {
            Id         = Guid.NewGuid(),
            Status     = status,
            SlotTime   = DateTimeOffset.UtcNow.AddHours(24),
            Patient    = patient,
            Provider   = provider,
            PatientId  = patient.Id,
            ProviderId = provider.Id,
        };
    }

    private static INotificationPreferenceChecker AllowAllPrefChecker()
    {
        var m = new Mock<INotificationPreferenceChecker>();
        m.Setup(c => c.IsAllowedAsync(
                It.IsAny<Guid>(), It.IsAny<NotificationChannel>(),
                It.IsAny<NotificationType>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(true);
        return m.Object;
    }

    private static AppointmentReminderJob BuildJob(IUnitOfWork uow, IEmailSender emailSender) =>
        new(uow, emailSender, new Mock<IInAppNotifier>().Object,
            AllowAllPrefChecker(),
            NullLogger<AppointmentReminderJob>.Instance,
            Options.Create(new ReminderSettings()));

    // ── tests ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.NoShow)]
    public async Task ExecuteAsync_TerminalStatus_DoesNotSendEmail(AppointmentStatus terminalStatus)
    {
        // Arrange
        var appointment = MakeAppointment(terminalStatus);
        var stubUow     = new AppointmentStubUnitOfWork(appointment);
        var mockSender  = new Mock<IEmailSender>();

        var job = BuildJob(stubUow, mockSender.Object);

        // Act
        await job.ExecuteAsync(appointment.Id);

        // Assert — no email sent for terminal states
        mockSender.Verify(
            s => s.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
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

        // Assert — email sent once to the patient's address
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
        // Arrange — UoW returns empty result
        var stubUow    = new AppointmentStubUnitOfWork(appointment: null);
        var mockSender = new Mock<IEmailSender>();
        var job        = BuildJob(stubUow, mockSender.Object);

        // Act — should log a warning and return, not throw
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
