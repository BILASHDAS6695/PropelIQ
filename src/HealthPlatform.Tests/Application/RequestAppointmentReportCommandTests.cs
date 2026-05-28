using HealthPlatform.Application.Features.PdfReport;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Moq;

namespace HealthPlatform.Tests.Application;

public sealed class RequestAppointmentReportCommandTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static PatientProfile MakeProfile(Guid userId) => new()
    {
        Id        = Guid.NewGuid(),
        UserId    = userId,
        FirstName = "Alice",
        LastName  = "Smith",
    };

    private static User MakeUser(Guid userId, UserRole role = UserRole.Patient) => new()
    {
        Id   = userId,
        Role = role,
    };

    private static Appointment MakeAppointment(Guid patientId) => new()
    {
        Id          = Guid.NewGuid(),
        PatientId   = patientId,
        SlotTime    = DateTimeOffset.UtcNow.AddDays(-1),
        Status      = AppointmentStatus.Completed,
        Provider    = new Provider { Id = Guid.NewGuid(), Name = "Dr. Test" },
        VisitReason = "Annual check-up",
    };

    // ── Builds a handler with controlled dependencies ─────────────────────

    private sealed class HandlerSetup
    {
        public Mock<IUnitOfWork>                                                    UowMock     { get; } = new();
        public Mock<IRepository<PatientProfile>>                                    ProfileRepo { get; } = new();
        public Mock<IRepository<User>>                                              UserRepo    { get; } = new();
        public Mock<IRepository<Appointment>>                                       AppRepo     { get; } = new();
        public Mock<IRepository<PdfReport>>          ReportRepo  { get; } = new();
        public Mock<IAppointmentReportBuilder>                                       Builder     { get; } = new();
        public Mock<IReportJobScheduler>                                             Jobs        { get; } = new();
        public Mock<ICurrentUserService>                                             CurrentUser { get; } = new();

        public RequestAppointmentReportCommandHandler Build()
        {
            UowMock.Setup(u => u.Repository<PatientProfile>()).Returns(ProfileRepo.Object);
            UowMock.Setup(u => u.Repository<User>()).Returns(UserRepo.Object);
            UowMock.Setup(u => u.Repository<Appointment>()).Returns(AppRepo.Object);
            UowMock.Setup(u => u.Repository<PdfReport>()).Returns(ReportRepo.Object);
            UowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(1);
            Builder.Setup(b => b.Build(It.IsAny<AppointmentReportData>()))
                   .Returns(new byte[] { 1, 2, 3 });

            return new RequestAppointmentReportCommandHandler(
                UowMock.Object,
                CurrentUser.Object,
                Builder.Object,
                Jobs.Object);
        }
    }

    // ── Tests ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_SyncPath_WhenAppointmentsLessThanOrEqual50()
    {
        var userId  = Guid.NewGuid();
        var profile = MakeProfile(userId);
        var setup   = new HandlerSetup();
        var handler = setup.Build();

        // 10 appointments — below the 50-threshold
        var appointments = Enumerable.Range(0, 10)
            .Select(_ => MakeAppointment(profile.Id))
            .ToList();

        setup.CurrentUser.Setup(c => c.IsAuthenticated).Returns(true);
        setup.CurrentUser.Setup(c => c.UserId).Returns(userId);
        setup.ProfileRepo
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<PatientProfile>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([profile]);
        setup.UserRepo
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<User>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeUser(userId)]);
        setup.ReportRepo
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<PdfReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        setup.AppRepo
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<Appointment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointments);
        setup.ReportRepo
            .Setup(r => r.AddAsync(It.IsAny<PdfReport>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cmd    = new RequestAppointmentReportCommand(profile.Id, DateTimeOffset.UtcNow.AddMonths(-12), DateTimeOffset.UtcNow);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsReady);
        setup.Builder.Verify(b => b.Build(It.IsAny<AppointmentReportData>()), Times.Once);
        // Hangfire should NOT have been called for the sync path
        setup.Jobs.Verify(j => j.EnqueueGenerate(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AsyncPath_WhenAppointmentsGreaterThan50()
    {
        var userId  = Guid.NewGuid();
        var profile = MakeProfile(userId);
        var setup   = new HandlerSetup();
        var handler = setup.Build();

        // 51 appointments — crosses the threshold
        var appointments = Enumerable.Range(0, 51)
            .Select(_ => MakeAppointment(profile.Id))
            .ToList();

        setup.CurrentUser.Setup(c => c.IsAuthenticated).Returns(true);
        setup.CurrentUser.Setup(c => c.UserId).Returns(userId);
        setup.ProfileRepo
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<PatientProfile>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([profile]);
        setup.UserRepo
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<User>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeUser(userId)]);
        setup.ReportRepo
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<PdfReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        setup.AppRepo
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<Appointment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointments);
        setup.ReportRepo
            .Setup(r => r.AddAsync(It.IsAny<PdfReport>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cmd    = new RequestAppointmentReportCommand(profile.Id, DateTimeOffset.UtcNow.AddMonths(-12), DateTimeOffset.UtcNow);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.False(result.IsReady);
        // Builder must NOT have been called (async path defers to Hangfire)
        setup.Builder.Verify(b => b.Build(It.IsAny<AppointmentReportData>()), Times.Never);
        // Job scheduler must have been called once with the new report's ID
        setup.Jobs.Verify(j => j.EnqueueGenerate(It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsCachedReport_WhenValidReportExists()
    {
        var userId  = Guid.NewGuid();
        var profile = MakeProfile(userId);
        var setup   = new HandlerSetup();
        var handler = setup.Build();

        var existingToken = Guid.NewGuid();
        var existingReport = new PdfReport
        {
            Id        = Guid.NewGuid(),
            PatientId = profile.Id,
            Token     = existingToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
            Status    = PdfReportStatus.Ready,
        };

        setup.CurrentUser.Setup(c => c.IsAuthenticated).Returns(true);
        setup.CurrentUser.Setup(c => c.UserId).Returns(userId);
        setup.ProfileRepo
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<PatientProfile>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([profile]);
        setup.UserRepo
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<User>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeUser(userId)]);
        // Dedup returns the existing report
        setup.ReportRepo
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<PdfReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([existingReport]);

        var cmd    = new RequestAppointmentReportCommand(profile.Id, DateTimeOffset.UtcNow.AddMonths(-12), DateTimeOffset.UtcNow);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal(existingToken, result.Token);
        Assert.True(result.IsReady);
        // No new appointments loaded, no new report created, no job enqueued
        setup.AppRepo.Verify(
            r => r.GetAsync(It.IsAny<ISpecification<Appointment>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        setup.Jobs.Verify(j => j.EnqueueGenerate(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ThrowsForbidden_WhenPatientRequestsAnotherPatientsReport()
    {
        var userId      = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var profile     = MakeProfile(otherUserId);  // profile owned by a different user
        var setup       = new HandlerSetup();
        var handler     = setup.Build();

        setup.CurrentUser.Setup(c => c.IsAuthenticated).Returns(true);
        setup.CurrentUser.Setup(c => c.UserId).Returns(userId);
        setup.ProfileRepo
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<PatientProfile>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([profile]);
        setup.UserRepo
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<User>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeUser(userId, UserRole.Patient)]);  // caller is a patient

        var cmd = new RequestAppointmentReportCommand(profile.Id, DateTimeOffset.UtcNow.AddMonths(-12), DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Succeeds_WhenStaffRequestsAnotherPatientsReport()
    {
        var staffUserId   = Guid.NewGuid();
        var patientUserId = Guid.NewGuid();
        var profile       = MakeProfile(patientUserId);
        var setup         = new HandlerSetup();
        var handler       = setup.Build();

        setup.CurrentUser.Setup(c => c.IsAuthenticated).Returns(true);
        setup.CurrentUser.Setup(c => c.UserId).Returns(staffUserId);
        setup.ProfileRepo
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<PatientProfile>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([profile]);
        setup.UserRepo
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<User>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeUser(staffUserId, UserRole.Staff)]);
        setup.ReportRepo
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<PdfReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        setup.AppRepo
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<Appointment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        setup.ReportRepo
            .Setup(r => r.AddAsync(It.IsAny<PdfReport>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cmd = new RequestAppointmentReportCommand(profile.Id, DateTimeOffset.UtcNow.AddMonths(-12), DateTimeOffset.UtcNow);

        // Should not throw
        var result = await handler.Handle(cmd, CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenPatientProfileDoesNotExist()
    {
        var setup   = new HandlerSetup();
        var handler = setup.Build();

        setup.CurrentUser.Setup(c => c.IsAuthenticated).Returns(true);
        setup.CurrentUser.Setup(c => c.UserId).Returns(Guid.NewGuid());
        setup.ProfileRepo
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<PatientProfile>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var cmd = new RequestAppointmentReportCommand(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMonths(-12), DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(cmd, CancellationToken.None));
    }
}
