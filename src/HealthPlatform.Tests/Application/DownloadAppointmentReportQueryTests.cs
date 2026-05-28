using HealthPlatform.Application.Features.PdfReport;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Moq;

namespace HealthPlatform.Tests.Application;

public sealed class DownloadAppointmentReportQueryTests
{
    private static DownloadAppointmentReportQueryHandler BuildHandler(
        Mock<IUnitOfWork>             uow,
        Mock<IRepository<PdfReport>>  repo,
        Mock<ICurrentUserService>     currentUser)
    {
        uow.Setup(u => u.Repository<PdfReport>()).Returns(repo.Object);
        return new DownloadAppointmentReportQueryHandler(uow.Object, currentUser.Object);
    }

    [Fact]
    public async Task Handle_ReturnsPdfBytes_WhenReportIsReady()
    {
        var patientId = Guid.NewGuid();
        var token     = Guid.NewGuid();
        var pdfBytes  = new byte[] { 37, 80, 68, 70 }; // %PDF magic bytes

        var report = new PdfReport
        {
            Id        = Guid.NewGuid(),
            PatientId = patientId,
            Token     = token,
            DateFrom  = DateTimeOffset.UtcNow.AddMonths(-3),
            DateTo    = DateTimeOffset.UtcNow,
            FileBytes = pdfBytes,
            Status    = PdfReportStatus.Ready,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
        };

        var uow         = new Mock<IUnitOfWork>();
        var repo        = new Mock<IRepository<PdfReport>>();
        var currentUser = new Mock<ICurrentUserService>();

        currentUser.Setup(c => c.IsAuthenticated).Returns(true);
        currentUser.Setup(c => c.UserId).Returns(Guid.NewGuid());
        repo.Setup(r => r.GetAsync(It.IsAny<ISpecification<PdfReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([report]);

        var handler = BuildHandler(uow, repo, currentUser);
        var result  = await handler.Handle(
            new DownloadAppointmentReportQuery(patientId, token), CancellationToken.None);

        Assert.Equal(pdfBytes, result.Bytes);
        Assert.Contains(".pdf", result.Filename);
    }

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenTokenDoesNotExist()
    {
        var uow         = new Mock<IUnitOfWork>();
        var repo        = new Mock<IRepository<PdfReport>>();
        var currentUser = new Mock<ICurrentUserService>();

        currentUser.Setup(c => c.IsAuthenticated).Returns(true);
        repo.Setup(r => r.GetAsync(It.IsAny<ISpecification<PdfReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler = BuildHandler(uow, repo, currentUser);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(
                new DownloadAppointmentReportQuery(Guid.NewGuid(), Guid.NewGuid()),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenReportHasExpired()
    {
        var report = new PdfReport
        {
            Id        = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            Token     = Guid.NewGuid(),
            FileBytes = new byte[] { 1, 2, 3 },
            Status    = PdfReportStatus.Ready,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        };

        var uow         = new Mock<IUnitOfWork>();
        var repo        = new Mock<IRepository<PdfReport>>();
        var currentUser = new Mock<ICurrentUserService>();

        currentUser.Setup(c => c.IsAuthenticated).Returns(true);
        repo.Setup(r => r.GetAsync(It.IsAny<ISpecification<PdfReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([report]);

        var handler = BuildHandler(uow, repo, currentUser);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(
                new DownloadAppointmentReportQuery(report.PatientId, report.Token),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenReportStatusIsPending()
    {
        var report = new PdfReport
        {
            Id        = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            Token     = Guid.NewGuid(),
            FileBytes = null,
            Status    = PdfReportStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(45),
        };

        var uow         = new Mock<IUnitOfWork>();
        var repo        = new Mock<IRepository<PdfReport>>();
        var currentUser = new Mock<ICurrentUserService>();

        currentUser.Setup(c => c.IsAuthenticated).Returns(true);
        repo.Setup(r => r.GetAsync(It.IsAny<ISpecification<PdfReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([report]);

        var handler = BuildHandler(uow, repo, currentUser);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(
                new DownloadAppointmentReportQuery(report.PatientId, report.Token),
                CancellationToken.None));
    }
}
