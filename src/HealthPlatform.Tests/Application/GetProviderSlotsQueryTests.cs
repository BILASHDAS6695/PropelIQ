using System.Linq.Expressions;
using HealthPlatform.Application;
using HealthPlatform.Application.Features.Providers;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace HealthPlatform.Tests.Application;

public class GetProviderSlotsQueryTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static ISender BuildSender(IUnitOfWork uow)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddScoped(_ => uow);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSlots_NoSlotsForDate_ReturnsEmptyList()
    {
        // Arrange
        var uow    = new StubUnitOfWork(Array.Empty<AppointmentSlot>());
        var sender = BuildSender(uow);
        var query  = new GetProviderSlotsQuery(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));

        // Act
        var result = await sender.Send(query);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSlots_AvailableSlotsOnDate_ReturnsMappedDtos()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var date       = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var slotStart  = new DateTimeOffset(date.Year, date.Month, date.Day, 9, 0, 0, TimeSpan.Zero);

        var slot = new AppointmentSlot
        {
            Id         = Guid.NewGuid(),
            ProviderId = providerId,
            StartTime  = slotStart,
            EndTime    = slotStart.AddMinutes(30),
            Status     = SlotStatus.Available
        };

        var uow    = new StubUnitOfWork(new[] { slot });
        var sender = BuildSender(uow);
        var query  = new GetProviderSlotsQuery(providerId, date);

        // Act
        var result = await sender.Send(query);

        // Assert
        Assert.Single(result);
        Assert.Equal(slot.Id,         result[0].SlotId);
        Assert.Equal(slot.ProviderId, result[0].ProviderId);
        Assert.Equal(slot.StartTime,  result[0].StartTime);
        Assert.Equal(slot.EndTime,    result[0].EndTime);
        Assert.Equal("Available",     result[0].Status);
    }

    [Fact]
    public async Task GetSlots_BookedSlotOnDate_IsExcludedFromResults()
    {
        // Arrange — booked slot should NOT appear in the available-slots query
        var providerId = Guid.NewGuid();
        var date       = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var slotStart  = new DateTimeOffset(date.Year, date.Month, date.Day, 10, 0, 0, TimeSpan.Zero);

        var bookedSlot = new AppointmentSlot
        {
            Id         = Guid.NewGuid(),
            ProviderId = providerId,
            StartTime  = slotStart,
            EndTime    = slotStart.AddMinutes(30),
            Status     = SlotStatus.Booked
        };

        // The stub applies the spec criteria — Booked slots are filtered out.
        var uow    = new StubUnitOfWork(new[] { bookedSlot });
        var sender = BuildSender(uow);
        var query  = new GetProviderSlotsQuery(providerId, date);

        // Act
        var result = await sender.Send(query);

        // Assert — Booked slot excluded
        Assert.Empty(result);
    }
}

// ── Stubs ─────────────────────────────────────────────────────────────────────

/// <summary>
/// In-memory <see cref="IUnitOfWork"/> stub that serves a fixed list of
/// <see cref="AppointmentSlot"/> records. Applies specification criteria
/// via compiled LINQ so tests reflect real filtering behaviour.
/// </summary>
internal sealed class StubUnitOfWork : IUnitOfWork
{
    private readonly IReadOnlyList<AppointmentSlot> _slots;

    public StubUnitOfWork(IEnumerable<AppointmentSlot> slots)
        => _slots = slots.ToList();

    public IRepository<T> Repository<T>() where T : class
    {
        if (typeof(T) == typeof(AppointmentSlot))
            return (IRepository<T>)(object)new StubSlotRepository(_slots);

        return new EmptyRepository<T>();
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    public void Dispose() { }
}

internal sealed class StubSlotRepository : IRepository<AppointmentSlot>
{
    private readonly IReadOnlyList<AppointmentSlot> _data;

    public StubSlotRepository(IReadOnlyList<AppointmentSlot> data) => _data = data;

    public Task<IReadOnlyList<AppointmentSlot>> GetAsync(
        ISpecification<AppointmentSlot> spec, CancellationToken ct = default)
    {
        var query = _data.AsQueryable();

        if (spec.Criteria is not null)
            query = query.Where(spec.Criteria);

        if (spec.OrderBy is not null)
            query = query.OrderBy(spec.OrderBy);

        IReadOnlyList<AppointmentSlot> result = query.ToList();
        return Task.FromResult(result);
    }

    public Task<AppointmentSlot?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_data.FirstOrDefault(x => x.Id == id));

    public Task<IReadOnlyList<AppointmentSlot>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<AppointmentSlot> result = _data.ToList();
        return Task.FromResult(result);
    }

    public Task<int> CountAsync(ISpecification<AppointmentSlot> spec, CancellationToken ct = default)
        => Task.FromResult(_data.Count);

    public Task AddAsync(AppointmentSlot entity, CancellationToken ct = default)
        => Task.CompletedTask;

    public void Update(AppointmentSlot entity) { }
    public void Delete(AppointmentSlot entity) { }
}

/// <summary>No-op repository for entity types not under test.</summary>
internal sealed class EmptyRepository<T> : IRepository<T> where T : class
{
    public Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult<T?>(null);

    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<T>>(Array.Empty<T>());

    public Task<IReadOnlyList<T>> GetAsync(ISpecification<T> spec, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<T>>(Array.Empty<T>());

    public Task<int> CountAsync(ISpecification<T> spec, CancellationToken ct = default)
        => Task.FromResult(0);

    public Task AddAsync(T entity, CancellationToken ct = default) => Task.CompletedTask;
    public void Update(T entity) { }
    public void Delete(T entity) { }
}
