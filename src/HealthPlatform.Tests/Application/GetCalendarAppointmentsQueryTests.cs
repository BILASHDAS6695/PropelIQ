using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Moq;

namespace HealthPlatform.Tests.Application;

public sealed class GetCalendarAppointmentsQueryTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Appointment MakeAppointment(Guid patientId, Guid providerId) => new()
    {
        Id          = Guid.NewGuid(),
        PatientId   = patientId,
        ProviderId  = providerId,
        SlotTime    = DateTimeOffset.UtcNow.AddDays(1),
        Status      = AppointmentStatus.Scheduled,
        Provider    = new Provider { Id = providerId, Name = "Dr. Test" },
        Patient     = new PatientProfile
        {
            Id        = patientId,
            FirstName = "Jane",
            LastName  = "Doe",
        },
        VisitReason = null,
    };

    private static (
        Mock<IUnitOfWork> uow,
        Mock<IRepository<User>> userRepo,
        Mock<IRepository<PatientProfile>> profileRepo,
        Mock<IRepository<Appointment>> apptRepo,
        Mock<ICurrentUserService> currentUser)
    BuildSetup(Guid userId)
    {
        var uow         = new Mock<IUnitOfWork>();
        var userRepo    = new Mock<IRepository<User>>();
        var profileRepo = new Mock<IRepository<PatientProfile>>();
        var apptRepo    = new Mock<IRepository<Appointment>>();
        var currentUser = new Mock<ICurrentUserService>();

        currentUser.Setup(c => c.IsAuthenticated).Returns(true);
        currentUser.Setup(c => c.UserId).Returns(userId);

        uow.Setup(u => u.Repository<User>()).Returns(userRepo.Object);
        uow.Setup(u => u.Repository<PatientProfile>()).Returns(profileRepo.Object);
        uow.Setup(u => u.Repository<Appointment>()).Returns(apptRepo.Object);

        return (uow, userRepo, profileRepo, apptRepo, currentUser);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsOwnAppointments_WhenCallerIsPatient()
    {
        var userId      = Guid.NewGuid();
        var patientId   = Guid.NewGuid();
        var providerId  = Guid.NewGuid();
        var appointment = MakeAppointment(patientId, providerId);

        var (uow, userRepo, profileRepo, apptRepo, currentUser) = BuildSetup(userId);

        userRepo.Setup(r => r.GetAsync(It.IsAny<ISpecification<User>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new User { Id = userId, Role = UserRole.Patient }]);

        profileRepo.Setup(r => r.GetAsync(It.IsAny<ISpecification<PatientProfile>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PatientProfile { Id = patientId, UserId = userId, FirstName = "Jane", LastName = "Doe" }]);

        apptRepo.Setup(r => r.GetAsync(It.IsAny<ISpecification<Appointment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([appointment]);

        var handler = new GetCalendarAppointmentsQueryHandler(uow.Object, currentUser.Object);
        var result  = await handler.Handle(
            new GetCalendarAppointmentsQuery(DateTimeOffset.UtcNow.AddMonths(-1), DateTimeOffset.UtcNow),
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(appointment.Id, result[0].AppointmentId);
        Assert.Equal("Dr. Test",     result[0].ProviderName);
        Assert.Equal("Jane Doe",     result[0].PatientName);
    }

    [Fact]
    public async Task Handle_ReturnsProviderAppointments_WhenCallerIsStaff()
    {
        var staffUserId = Guid.NewGuid();
        var providerId  = Guid.NewGuid();
        var appointment = MakeAppointment(Guid.NewGuid(), providerId);

        var (uow, userRepo, _, apptRepo, currentUser) = BuildSetup(staffUserId);

        userRepo.Setup(r => r.GetAsync(It.IsAny<ISpecification<User>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new User { Id = staffUserId, Role = UserRole.Staff }]);

        apptRepo.Setup(r => r.GetAsync(It.IsAny<ISpecification<Appointment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([appointment]);

        var handler = new GetCalendarAppointmentsQueryHandler(uow.Object, currentUser.Object);
        var result  = await handler.Handle(
            new GetCalendarAppointmentsQuery(
                DateTimeOffset.UtcNow.AddMonths(-1),
                DateTimeOffset.UtcNow,
                providerId),
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(appointment.Id, result[0].AppointmentId);
        // Staff should NOT have queried patient profiles
        uow.Verify(u => u.Repository<PatientProfile>(), Times.Never);
    }
}
