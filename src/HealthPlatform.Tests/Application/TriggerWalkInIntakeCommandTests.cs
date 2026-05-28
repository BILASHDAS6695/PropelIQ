using HealthPlatform.Application.Features.Intake;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Moq;

namespace HealthPlatform.Tests.Application;

public class TriggerWalkInIntakeCommandTests
{
    [Fact]
    public async Task Handle_WhenNoDraftExists_CreatesNewDraftRecord()
    {
        // Arrange
        var appointmentId = Guid.NewGuid();
        var patientId     = Guid.NewGuid();

        var intakeRepoMock = new Mock<IRepository<IntakeRecord>>();
        var apptRepoMock   = new Mock<IRepository<Appointment>>();
        var uowMock        = new Mock<IUnitOfWork>();

        intakeRepoMock
            .Setup(r => r.GetAsync(
                It.IsAny<ISpecification<IntakeRecord>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var appt = new Appointment { Id = appointmentId, PatientId = patientId, IsWalkIn = true };
        apptRepoMock
            .Setup(r => r.GetAsync(
                It.IsAny<ISpecification<Appointment>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([appt]);

        uowMock.Setup(u => u.Repository<IntakeRecord>()).Returns(intakeRepoMock.Object);
        uowMock.Setup(u => u.Repository<Appointment>()).Returns(apptRepoMock.Object);

        IntakeRecord? saved = null;
        intakeRepoMock
            .Setup(r => r.AddAsync(It.IsAny<IntakeRecord>(), It.IsAny<CancellationToken>()))
            .Callback<IntakeRecord, CancellationToken>((r, _) => saved = r)
            .Returns(Task.CompletedTask);

        var handler = new TriggerWalkInIntakeCommandHandler(uowMock.Object);
        var cmd     = new TriggerWalkInIntakeCommand(appointmentId, Guid.NewGuid());

        // Act
        await handler.Handle(cmd, CancellationToken.None);

        // Assert
        Assert.NotNull(saved);
        Assert.Equal(IntakeStatus.Draft,    saved!.Status);
        Assert.Equal(IntakeMode.ManualForm, saved.Mode);
        Assert.Equal(patientId,             saved.PatientId);
        Assert.Equal(appointmentId,         saved.AppointmentId);
    }

    [Fact]
    public async Task Handle_WhenDraftAlreadyExists_ReturnsExistingIdWithoutCreating()
    {
        // Arrange
        var existingId    = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();

        var existing = new IntakeRecord
        {
            Id            = existingId,
            AppointmentId = appointmentId,
            Status        = IntakeStatus.Draft,
        };

        var intakeRepoMock = new Mock<IRepository<IntakeRecord>>();
        var uowMock        = new Mock<IUnitOfWork>();

        intakeRepoMock
            .Setup(r => r.GetAsync(
                It.IsAny<ISpecification<IntakeRecord>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([existing]);

        uowMock.Setup(u => u.Repository<IntakeRecord>()).Returns(intakeRepoMock.Object);

        var handler = new TriggerWalkInIntakeCommandHandler(uowMock.Object);
        var cmd     = new TriggerWalkInIntakeCommand(appointmentId, Guid.NewGuid());

        // Act
        var result = await handler.Handle(cmd, CancellationToken.None);

        // Assert — idempotent: returns existing ID, no AddAsync called
        Assert.Equal(existingId, result);
        intakeRepoMock.Verify(
            r => r.AddAsync(It.IsAny<IntakeRecord>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
