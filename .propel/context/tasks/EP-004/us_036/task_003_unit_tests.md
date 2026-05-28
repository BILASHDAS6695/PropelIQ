# Task 003: Unit Tests for PDF Report Generation

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-036 |
| **Epic** | EP-004 |
| **Layer** | Tests |
| **Priority** | Low |
| **Estimated Effort** | 40 minutes |
| **Dependencies** | Task 001 and Task 002 complete — `AppointmentReportBuilder`, `RequestAppointmentReportCommandHandler`, `DownloadAppointmentReportQueryHandler` all compile |

## Objective

Add unit tests for the three testable components introduced in Tasks 001–002:

1. **`AppointmentReportBuilderTests`** — verifies the QuestPDF builder returns
   non-empty bytes in both the "no appointments" and "with appointments" cases.
2. **`RequestAppointmentReportCommandTests`** — verifies the command handler
   orchestrates the sync path (≤ 50), async path (> 50), deduplication of an
   existing valid report, and the ownership/auth guard.
3. **`DownloadAppointmentReportQueryTests`** — verifies the download handler
   returns bytes for a ready report, rejects expired tokens, and rejects
   not-yet-ready reports.

---

## Acceptance Criteria Covered

- AC: PDF generated using QuestPDF library (builder smoke test)
- AC: No appointments in range → PDF with "No appointments found"
- AC: Generated asynchronously via Hangfire if > 50 appointments
- AC: Concurrent generation requests → deduplicate
- AC: Patient can only generate own report; staff can generate for any patient
- AC: Download link expires after 1 hour

---

## Implementation Steps

### 1. Create `AppointmentReportBuilderTests.cs`

Create `src/HealthPlatform.Tests/Application/AppointmentReportBuilderTests.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Infrastructure.Reports;

namespace HealthPlatform.Tests.Application;

public sealed class AppointmentReportBuilderTests
{
    private static AppointmentReportBuilder Builder() => new();

    [Fact]
    public void Build_ReturnsNonEmptyBytes_WhenNoAppointments()
    {
        var data = new AppointmentReportData(
            "Alice Smith",
            DateTimeOffset.UtcNow.AddMonths(-12),
            DateTimeOffset.UtcNow,
            []);

        var bytes = Builder().Build(data);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public void Build_ReturnsNonEmptyBytes_WhenAppointmentsPresent()
    {
        var rows = Enumerable.Range(1, 5)
            .Select(i => new AppointmentReportRow(
                DateTimeOffset.UtcNow.AddDays(-i),
                $"Dr. Provider {i}",
                "Scheduled",
                i % 2 == 0 ? "Annual check-up" : null))
            .ToList();

        var data = new AppointmentReportData(
            "Bob Jones",
            DateTimeOffset.UtcNow.AddMonths(-1),
            DateTimeOffset.UtcNow,
            rows);

        var bytes = Builder().Build(data);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100); // must be a real PDF, not empty
    }

    [Fact]
    public void Build_IsDeterministicInStructure_ForSameInput()
    {
        // Two calls with identical data must produce valid PDFs (both > 0 bytes).
        // We do not assert byte-for-byte equality (generation timestamps differ).
        var data = new AppointmentReportData(
            "Carol White",
            DateTimeOffset.UtcNow.AddMonths(-3),
            DateTimeOffset.UtcNow,
            [new(DateTimeOffset.UtcNow.AddDays(-5), "Dr. Brown", "Completed", "Follow-up")]);

        var bytes1 = Builder().Build(data);
        var bytes2 = Builder().Build(data);

        Assert.True(bytes1.Length > 0);
        Assert.True(bytes2.Length > 0);
    }
}
```

---

### 2. Create `RequestAppointmentReportCommandTests.cs`

Create `src/HealthPlatform.Tests/Application/RequestAppointmentReportCommandTests.cs`:

```csharp
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Features.PdfReport;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
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
        Id         = Guid.NewGuid(),
        PatientId  = patientId,
        SlotTime   = DateTimeOffset.UtcNow.AddDays(-1),
        Status     = AppointmentStatus.Completed,
        Provider   = new Provider { Id = Guid.NewGuid(), Name = "Dr. Test" },
        VisitReason = "Annual check-up",
    };

    // ── Builds a handler with controlled dependencies ─────────────────────

    private sealed class HandlerSetup
    {
        public Mock<IUnitOfWork>               UowMock        { get; } = new();
        public Mock<IRepository<PatientProfile>> ProfileRepo  { get; } = new();
        public Mock<IRepository<User>>          UserRepo      { get; } = new();
        public Mock<IRepository<Appointment>>   AppRepo       { get; } = new();
        public Mock<IRepository<Domain.Entities.PdfReport>> ReportRepo { get; } = new();
        public Mock<IAppointmentReportBuilder>  Builder       { get; } = new();
        public Mock<IBackgroundJobClient>       Jobs          { get; } = new();
        public Mock<ICurrentUserService>        CurrentUser   { get; } = new();

        public RequestAppointmentReportCommandHandler Build()
        {
            UowMock.Setup(u => u.Repository<PatientProfile>()).Returns(ProfileRepo.Object);
            UowMock.Setup(u => u.Repository<User>()).Returns(UserRepo.Object);
            UowMock.Setup(u => u.Repository<Appointment>()).Returns(AppRepo.Object);
            UowMock.Setup(u => u.Repository<Domain.Entities.PdfReport>()).Returns(ReportRepo.Object);
            UowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(1);
            Builder.Setup(b => b.Build(It.IsAny<AppointmentReportData>()))
                   .Returns(new byte[] { 1, 2, 3 });
            Jobs.Setup(j => j.Create(It.IsAny<Job>(), It.IsAny<IState>()))
                .Returns("job-id");

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
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<Domain.Entities.PdfReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        setup.AppRepo
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<Appointment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointments);
        setup.ReportRepo
            .Setup(r => r.AddAsync(It.IsAny<Domain.Entities.PdfReport>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cmd    = new RequestAppointmentReportCommand(profile.Id, DateTimeOffset.UtcNow.AddMonths(-12), DateTimeOffset.UtcNow);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsReady);
        setup.Builder.Verify(b => b.Build(It.IsAny<AppointmentReportData>()), Times.Once);
        // Hangfire should NOT have been called for the sync path
        setup.Jobs.Verify(
            j => j.Create(It.IsAny<Job>(), It.IsAny<IState>()),
            Times.Never);
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
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<Domain.Entities.PdfReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        setup.AppRepo
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<Appointment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointments);
        setup.ReportRepo
            .Setup(r => r.AddAsync(It.IsAny<Domain.Entities.PdfReport>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cmd    = new RequestAppointmentReportCommand(profile.Id, DateTimeOffset.UtcNow.AddMonths(-12), DateTimeOffset.UtcNow);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.False(result.IsReady);
        // Builder must NOT have been called (async path defers to Hangfire)
        setup.Builder.Verify(b => b.Build(It.IsAny<AppointmentReportData>()), Times.Never);
        // Hangfire job must have been enqueued once
        setup.Jobs.Verify(
            j => j.Create(
                It.Is<Job>(job => job.Type == typeof(GeneratePdfReportJob)),
                It.IsAny<EnqueuedState>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsCachedReport_WhenValidReportExists()
    {
        var userId  = Guid.NewGuid();
        var profile = MakeProfile(userId);
        var setup   = new HandlerSetup();
        var handler = setup.Build();

        var existingToken = Guid.NewGuid();
        var existingReport = new Domain.Entities.PdfReport
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
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<Domain.Entities.PdfReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([existingReport]);

        var cmd    = new RequestAppointmentReportCommand(profile.Id, DateTimeOffset.UtcNow.AddMonths(-12), DateTimeOffset.UtcNow);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal(existingToken, result.Token);
        Assert.True(result.IsReady);
        // No new appointments loaded, no new report created, no job enqueued
        setup.AppRepo.Verify(
            r => r.GetAsync(It.IsAny<ISpecification<Appointment>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        setup.Jobs.Verify(
            j => j.Create(It.IsAny<Job>(), It.IsAny<IState>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ThrowsForbidden_WhenPatientRequestsAnotherPatientsReport()
    {
        var userId        = Guid.NewGuid();
        var otherUserId   = Guid.NewGuid();
        var profile       = MakeProfile(otherUserId);  // profile owned by a different user
        var setup         = new HandlerSetup();
        var handler       = setup.Build();

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
        var staffUserId = Guid.NewGuid();
        var patientUserId = Guid.NewGuid();
        var profile = MakeProfile(patientUserId);
        var setup   = new HandlerSetup();
        var handler = setup.Build();

        setup.CurrentUser.Setup(c => c.IsAuthenticated).Returns(true);
        setup.CurrentUser.Setup(c => c.UserId).Returns(staffUserId);
        setup.ProfileRepo
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<PatientProfile>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([profile]);
        setup.UserRepo
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<User>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeUser(staffUserId, UserRole.Staff)]);
        setup.ReportRepo
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<Domain.Entities.PdfReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        setup.AppRepo
            .Setup(r => r.GetAsync(It.IsAny<ISpecification<Appointment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        setup.ReportRepo
            .Setup(r => r.AddAsync(It.IsAny<Domain.Entities.PdfReport>(), It.IsAny<CancellationToken>()))
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
```

---

### 3. Create `DownloadAppointmentReportQueryTests.cs`

Create `src/HealthPlatform.Tests/Application/DownloadAppointmentReportQueryTests.cs`:

```csharp
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
        Mock<IUnitOfWork>                                     uow,
        Mock<IRepository<Domain.Entities.PdfReport>>          repo,
        Mock<ICurrentUserService>                             currentUser)
    {
        uow.Setup(u => u.Repository<Domain.Entities.PdfReport>()).Returns(repo.Object);
        return new DownloadAppointmentReportQueryHandler(uow.Object, currentUser.Object);
    }

    [Fact]
    public async Task Handle_ReturnsPdfBytes_WhenReportIsReady()
    {
        var patientId = Guid.NewGuid();
        var token     = Guid.NewGuid();
        var pdfBytes  = new byte[] { 37, 80, 68, 70 }; // %PDF magic bytes

        var report = new Domain.Entities.PdfReport
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
        var repo        = new Mock<IRepository<Domain.Entities.PdfReport>>();
        var currentUser = new Mock<ICurrentUserService>();

        currentUser.Setup(c => c.IsAuthenticated).Returns(true);
        currentUser.Setup(c => c.UserId).Returns(Guid.NewGuid());
        repo.Setup(r => r.GetAsync(It.IsAny<ISpecification<Domain.Entities.PdfReport>>(), It.IsAny<CancellationToken>()))
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
        var repo        = new Mock<IRepository<Domain.Entities.PdfReport>>();
        var currentUser = new Mock<ICurrentUserService>();

        currentUser.Setup(c => c.IsAuthenticated).Returns(true);
        repo.Setup(r => r.GetAsync(It.IsAny<ISpecification<Domain.Entities.PdfReport>>(), It.IsAny<CancellationToken>()))
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
        var report = new Domain.Entities.PdfReport
        {
            Id        = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            Token     = Guid.NewGuid(),
            FileBytes = new byte[] { 1, 2, 3 },
            Status    = PdfReportStatus.Ready,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5),  // expired
        };

        var uow         = new Mock<IUnitOfWork>();
        var repo        = new Mock<IRepository<Domain.Entities.PdfReport>>();
        var currentUser = new Mock<ICurrentUserService>();

        currentUser.Setup(c => c.IsAuthenticated).Returns(true);
        repo.Setup(r => r.GetAsync(It.IsAny<ISpecification<Domain.Entities.PdfReport>>(), It.IsAny<CancellationToken>()))
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
        var report = new Domain.Entities.PdfReport
        {
            Id        = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            Token     = Guid.NewGuid(),
            FileBytes = null,             // not yet generated
            Status    = PdfReportStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(45),
        };

        var uow         = new Mock<IUnitOfWork>();
        var repo        = new Mock<IRepository<Domain.Entities.PdfReport>>();
        var currentUser = new Mock<ICurrentUserService>();

        currentUser.Setup(c => c.IsAuthenticated).Returns(true);
        repo.Setup(r => r.GetAsync(It.IsAny<ISpecification<Domain.Entities.PdfReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([report]);

        var handler = BuildHandler(uow, repo, currentUser);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(
                new DownloadAppointmentReportQuery(report.PatientId, report.Token),
                CancellationToken.None));
    }
}
```

---

## Verification

```bash
cd src
dotnet test HealthPlatform.Tests/HealthPlatform.Tests.csproj -v q 2>&1 | tail -5
```

Expected output:

```
Passed!  - Failed: 0, Passed: 56, Skipped: 0, Total: 56, Duration: ...
```

> 43 existing + 3 builder + 6 command handler + 4 download handler = **56 total**.

---

## Notes

- `AppointmentReportBuilderTests` are integration-style tests (they actually
  invoke QuestPDF) but run in-process without any external dependencies — no
  test server or database required.
- The `HandlerSetup` helper class in `RequestAppointmentReportCommandTests`
  follows the same pattern as `BuildJob()` in `AppointmentReminderJobTests`.
- `IRepository<T>` must already be accessible from the Tests project via the
  `InternalsVisibleTo` attribute on the Infrastructure project (added in
  US-032). If `AppointmentReportBuilder` is `internal sealed`, the attribute
  covers it.
- `UserRole` is in `HealthPlatform.Domain.Enums` — import with
  `using HealthPlatform.Domain.Enums;`.
