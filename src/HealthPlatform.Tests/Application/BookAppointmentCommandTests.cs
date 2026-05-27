using HealthPlatform.Application;
using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace HealthPlatform.Tests.Application;

public class BookAppointmentCommandTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

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
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Book_UnauthenticatedUser_ThrowsUnauthorizedAccessException()
    {
        var sender = BuildSender(
            new BookingStubUnitOfWork(null, null),
            new AnonymousBookingUser(),
            new NoOpBookingEmailSender());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => sender.Send(new BookAppointmentCommand(Guid.NewGuid())));
    }

    [Fact]
    public async Task Book_UnavailableSlot_ThrowsConflictException()
    {
        // Arrange — slot is already Booked
        var userId  = Guid.NewGuid();
        var patient = new PatientProfile { Id = Guid.NewGuid(), UserId = userId };
        var slot = new AppointmentSlot
        {
            Id         = Guid.NewGuid(),
            ProviderId = Guid.NewGuid(),
            StartTime  = DateTimeOffset.UtcNow.AddDays(1),
            EndTime    = DateTimeOffset.UtcNow.AddDays(1).AddMinutes(30),
            Status     = SlotStatus.Booked
        };

        var sender = BuildSender(
            new BookingStubUnitOfWork(patient, slot),
            new AuthenticatedBookingUser(userId),
            new NoOpBookingEmailSender());

        // Act & Assert — handler should throw ConflictException (→ HTTP 409)
        await Assert.ThrowsAsync<ConflictException>(
            () => sender.Send(new BookAppointmentCommand(slot.Id)));
    }
}

// ── Stubs ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Stub UnitOfWork that serves a single <see cref="PatientProfile"/> and a
/// single <see cref="AppointmentSlot"/>. All other repositories return empty.
/// </summary>
internal sealed class BookingStubUnitOfWork : IUnitOfWork
{
    private readonly PatientProfile?  _patient;
    private readonly AppointmentSlot? _slot;

    public BookingStubUnitOfWork(PatientProfile? patient, AppointmentSlot? slot)
    {
        _patient = patient;
        _slot    = slot;
    }

    public IRepository<T> Repository<T>() where T : class
    {
        if (typeof(T) == typeof(PatientProfile))
            return (IRepository<T>)(object)new PatientProfileStubRepo(_patient);

        if (typeof(T) == typeof(AppointmentSlot))
            return (IRepository<T>)(object)new AppointmentSlotStubRepo(_slot);

        return new EmptyRepository<T>();
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    public void Dispose() { }
}

/// <summary>Returns a single PatientProfile for any GetAsync call.</summary>
internal sealed class PatientProfileStubRepo : IRepository<PatientProfile>
{
    private readonly PatientProfile? _patient;

    public PatientProfileStubRepo(PatientProfile? patient) => _patient = patient;

    public Task<PatientProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_patient?.Id == id ? _patient : null);

    public Task<IReadOnlyList<PatientProfile>> GetAsync(
        ISpecification<PatientProfile> spec, CancellationToken ct = default)
    {
        IReadOnlyList<PatientProfile> result = _patient is null
            ? Array.Empty<PatientProfile>()
            : [_patient];
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<PatientProfile>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<PatientProfile> result = _patient is null
            ? Array.Empty<PatientProfile>()
            : [_patient];
        return Task.FromResult(result);
    }

    public Task<int> CountAsync(ISpecification<PatientProfile> spec, CancellationToken ct = default)
        => Task.FromResult(_patient is null ? 0 : 1);

    public Task AddAsync(PatientProfile entity, CancellationToken ct = default) => Task.CompletedTask;
    public void Update(PatientProfile entity) { }
    public void Delete(PatientProfile entity) { }
}

/// <summary>Returns a single AppointmentSlot for GetByIdAsync calls.</summary>
internal sealed class AppointmentSlotStubRepo : IRepository<AppointmentSlot>
{
    private readonly AppointmentSlot? _slot;

    public AppointmentSlotStubRepo(AppointmentSlot? slot) => _slot = slot;

    public Task<AppointmentSlot?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_slot?.Id == id ? _slot : null);

    public Task<IReadOnlyList<AppointmentSlot>> GetAsync(
        ISpecification<AppointmentSlot> spec, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AppointmentSlot>>(Array.Empty<AppointmentSlot>());

    public Task<IReadOnlyList<AppointmentSlot>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AppointmentSlot>>(Array.Empty<AppointmentSlot>());

    public Task<int> CountAsync(ISpecification<AppointmentSlot> spec, CancellationToken ct = default)
        => Task.FromResult(0);

    public Task AddAsync(AppointmentSlot entity, CancellationToken ct = default) => Task.CompletedTask;
    public void Update(AppointmentSlot entity) { }
    public void Delete(AppointmentSlot entity) { }
}

internal sealed class AuthenticatedBookingUser : ICurrentUserService
{
    public AuthenticatedBookingUser(Guid userId) => UserId = userId;
    public Guid? UserId          { get; }
    public bool  IsAuthenticated => true;
}

internal sealed class AnonymousBookingUser : ICurrentUserService
{
    public Guid? UserId          => null;
    public bool  IsAuthenticated => false;
}

internal sealed class NoOpBookingEmailSender : IEmailSender
{
    public Task SendAsync(string toAddress, string subject, string body, CancellationToken ct = default)
        => Task.CompletedTask;
}
