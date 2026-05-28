# Task 001: Calendar Query, Specification & API Endpoint

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-037 |
| **Epic** | EP-005 |
| **Layer** | Application (CQRS) + API (controller endpoint) |
| **Priority** | High |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | None — builds on existing `GetMyAppointmentsQuery` / `TodayAppointmentsSearchSpecification` patterns |

## Objective

1. **Add `CalendarAppointmentDto`** + `GetCalendarAppointmentsQuery` — a date-range
   query that works for both patients (own appointments) and staff
   (all appointments for a given provider).
2. **Add `AppointmentsInDateRangeSpecification`** — patient-scoped date-range query
   with eager loads.
3. **Add `ProviderAppointmentsInDateRangeSpecification`** — provider-scoped date-range
   query with eager loads.
4. **Add `GET /api/appointments/calendar`** endpoint in `AppointmentsController`.
5. **Add 2 unit tests** confirming patient and staff data-access paths.

---

## Acceptance Criteria Covered

- AC: Patient calendar shows only their own appointments
- AC: Staff calendar shows all patients for selected provider
- AC: Month/week/day views — all share the same API endpoint (front-end decides view)

---

## Implementation Steps

### 1. Add `GetCalendarAppointmentsQuery` + DTO

Create `src/HealthPlatform.Application/Features/Appointments/GetCalendarAppointmentsQuery.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Returns appointments within a date range for the calendar view.
/// - Patient callers: returns own appointments (ProviderId filter ignored).
/// - Staff/Admin callers: returns all appointments for the given provider
///   (or all providers when ProviderId is null).
/// </summary>
public sealed record GetCalendarAppointmentsQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    Guid?          ProviderId = null) : IRequest<IReadOnlyList<CalendarAppointmentDto>>;

/// <summary>Appointment summary returned for calendar rendering.</summary>
public sealed record CalendarAppointmentDto(
    Guid           AppointmentId,
    Guid           ProviderId,
    string         ProviderName,
    string         PatientName,
    DateTimeOffset SlotTime,
    DateTimeOffset EndTime,
    string         Status,
    string?        VisitReason);
```

---

### 2. Add `AppointmentsInDateRangeSpecification`

Create `src/HealthPlatform.Application/Features/Appointments/AppointmentsInDateRangeSpecification.cs`:

```csharp
using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Returns all appointments for a patient within a date range, ordered by
/// SlotTime ascending. Eagerly loads Provider and Patient navigations.
/// </summary>
internal sealed class AppointmentsInDateRangeSpecification : ISpecification<Appointment>
{
    private readonly Guid           _patientId;
    private readonly DateTimeOffset _from;
    private readonly DateTimeOffset _to;

    public AppointmentsInDateRangeSpecification(
        Guid           patientId,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        _patientId = patientId;
        _from      = from;
        _to        = to;
    }

    public Expression<Func<Appointment, bool>>? Criteria =>
        a => a.PatientId == _patientId
          && a.SlotTime  >= _from
          && a.SlotTime  <= _to;

    public List<Expression<Func<Appointment, object>>> Includes =>
    [
        a => a.Provider,
        a => a.Patient,
    ];

    public Expression<Func<Appointment, object>>?      OrderBy           => a => a.SlotTime;
    public Expression<Func<Appointment, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
```

---

### 3. Add `ProviderAppointmentsInDateRangeSpecification`

Create `src/HealthPlatform.Application/Features/Appointments/ProviderAppointmentsInDateRangeSpecification.cs`:

```csharp
using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Returns all appointments for a provider (or all providers) within a date
/// range, ordered by SlotTime ascending. Used by staff/admin calendar view.
/// Eagerly loads Provider and Patient navigations.
/// </summary>
internal sealed class ProviderAppointmentsInDateRangeSpecification : ISpecification<Appointment>
{
    private readonly Guid?          _providerId;
    private readonly DateTimeOffset _from;
    private readonly DateTimeOffset _to;

    public ProviderAppointmentsInDateRangeSpecification(
        Guid?          providerId,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        _providerId = providerId;
        _from       = from;
        _to         = to;
    }

    public Expression<Func<Appointment, bool>>? Criteria =>
        a => a.SlotTime >= _from
          && a.SlotTime <= _to
          && (_providerId == null || a.ProviderId == _providerId);

    public List<Expression<Func<Appointment, object>>> Includes =>
    [
        a => a.Provider,
        a => a.Patient,
    ];

    public Expression<Func<Appointment, object>>?      OrderBy           => a => a.SlotTime;
    public Expression<Func<Appointment, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
```

---

### 4. Add `GetCalendarAppointmentsQueryHandler`

Create `src/HealthPlatform.Application/Features/Appointments/GetCalendarAppointmentsQueryHandler.cs`:

```csharp
using HealthPlatform.Application.Features.Auth;
using HealthPlatform.Application.Features.Patients;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

internal sealed class GetCalendarAppointmentsQueryHandler
    : IRequestHandler<GetCalendarAppointmentsQuery, IReadOnlyList<CalendarAppointmentDto>>
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;

    public GetCalendarAppointmentsQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow         = uow;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<CalendarAppointmentDto>> Handle(
        GetCalendarAppointmentsQuery query,
        CancellationToken            ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("User must be authenticated.");

        var callerUsers = await _uow.Repository<User>()
            .GetAsync(new UserByIdSpecification(_currentUser.UserId.Value), ct);

        var isStaffOrAdmin = callerUsers.Count > 0
            && callerUsers[0].Role is UserRole.Staff or UserRole.Admin;

        IReadOnlyList<Appointment> appointments;

        if (isStaffOrAdmin)
        {
            // Staff path: return all appointments for the given provider (or all if null)
            appointments = await _uow.Repository<Appointment>()
                .GetAsync(
                    new ProviderAppointmentsInDateRangeSpecification(
                        query.ProviderId, query.From, query.To),
                    ct);
        }
        else
        {
            // Patient path: resolve patient profile and return own appointments only
            var profiles = await _uow.Repository<PatientProfile>()
                .GetAsync(new PatientProfileByUserIdSpecification(_currentUser.UserId.Value), ct);

            if (profiles.Count == 0)
                throw new NotFoundException(nameof(PatientProfile), _currentUser.UserId.Value);

            appointments = await _uow.Repository<Appointment>()
                .GetAsync(
                    new AppointmentsInDateRangeSpecification(profiles[0].Id, query.From, query.To),
                    ct);
        }

        return appointments
            .Select(a => new CalendarAppointmentDto(
                AppointmentId: a.Id,
                ProviderId:    a.ProviderId,
                ProviderName:  a.Provider.Name,
                PatientName:   $"{a.Patient.FirstName} {a.Patient.LastName}",
                SlotTime:      a.SlotTime,
                EndTime:       a.Slot?.EndTime ?? a.SlotTime.AddMinutes(30),
                Status:        a.Status.ToString(),
                VisitReason:   a.VisitReason))
            .ToList()
            .AsReadOnly();
    }
}
```

---

### 5. Add API endpoint to `AppointmentsController`

File: `src/HealthPlatform.Api/Controllers/AppointmentsController.cs`

Add after the `GetMine` endpoint:

```csharp
/// <summary>
/// Returns appointments within a date range for the calendar view.
/// Patients receive only their own appointments.
/// Staff/Admin can optionally filter by <paramref name="providerId"/>.
/// </summary>
/// <param name="from">Range start (ISO 8601). Defaults to start of the current month.</param>
/// <param name="to">Range end (ISO 8601). Defaults to end of the current month.</param>
/// <param name="providerId">Optional provider filter (staff/admin only).</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>
/// 200 OK — list of <see cref="CalendarAppointmentDto"/> ordered by SlotTime ascending.<br/>
/// 401 Unauthorized — user not authenticated.
/// </returns>
[HttpGet("calendar")]
[Authorize]
[ProducesResponseType(typeof(IReadOnlyList<CalendarAppointmentDto>), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public async Task<IActionResult> GetCalendar(
    [FromQuery] DateTimeOffset? from,
    [FromQuery] DateTimeOffset? to,
    [FromQuery] Guid?           providerId,
    CancellationToken           ct)
{
    var now       = DateTimeOffset.UtcNow;
    var dateFrom  = from ?? new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
    var dateTo    = to   ?? dateFrom.AddMonths(1).AddTicks(-1);

    var results = await _sender.Send(
        new GetCalendarAppointmentsQuery(dateFrom, dateTo, providerId), ct);

    return Ok(results);
}
```

---

### 6. Add unit tests

Create `src/HealthPlatform.Tests/Application/GetCalendarAppointmentsQueryTests.cs`:

```csharp
using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Features.Auth;
using HealthPlatform.Application.Features.Patients;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Moq;

namespace HealthPlatform.Tests.Application;

public sealed class GetCalendarAppointmentsQueryTests
{
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

    [Fact]
    public async Task Handle_ReturnsOwnAppointments_WhenCallerIsPatient()
    {
        var userId    = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var appointment = MakeAppointment(patientId, Guid.NewGuid());

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
    }

    [Fact]
    public async Task Handle_ReturnsProviderAppointments_WhenCallerIsStaff()
    {
        var staffUserId = Guid.NewGuid();
        var providerId  = Guid.NewGuid();
        var appointment = MakeAppointment(Guid.NewGuid(), providerId);

        var uow         = new Mock<IUnitOfWork>();
        var userRepo    = new Mock<IRepository<User>>();
        var apptRepo    = new Mock<IRepository<Appointment>>();
        var currentUser = new Mock<ICurrentUserService>();

        currentUser.Setup(c => c.IsAuthenticated).Returns(true);
        currentUser.Setup(c => c.UserId).Returns(staffUserId);

        uow.Setup(u => u.Repository<User>()).Returns(userRepo.Object);
        uow.Setup(u => u.Repository<Appointment>()).Returns(apptRepo.Object);

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
```

---

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --no-incremental -v q 2>&1 | Select-String "error" | Select-Object -First 10
dotnet test HealthPlatform.Tests/HealthPlatform.Tests.csproj -v q 2>&1 | Select-String "passed|failed" | Select-Object -Last 3
```

Expected: build clean, 58/58 tests passing (56 existing + 2 new).

---

## Notes

- `Slot?.EndTime` follows the same null-coalescence pattern used in `GetMyAppointmentsQueryHandler`.
- `PatientProfileByUserIdSpecification` already exists in `Features/Appointments/`.
- `UserByIdSpecification` already exists in `Features/Auth/` (created in US-036 Task 002).
- No new migration needed — no schema changes.
