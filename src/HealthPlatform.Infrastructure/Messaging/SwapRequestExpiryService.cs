using HealthPlatform.Application.Features.SlotSwap;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Infrastructure.Messaging;

/// <summary>
/// Periodic background sweep that expires <see cref="SlotSwapRequest"/> rows
/// whose <c>ExpiresAt</c> has passed while still in <c>Pending</c> status.
/// Runs every <see cref="SweepInterval"/> minutes.
/// </summary>
internal sealed class SwapRequestExpiryService : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory              _scopeFactory;
    private readonly ILogger<SwapRequestExpiryService> _logger;

    public SwapRequestExpiryService(
        IServiceScopeFactory              scopeFactory,
        ILogger<SwapRequestExpiryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SwapRequestExpiryService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "SwapRequestExpiryService sweep failed.");
            }

            await Task.Delay(SweepInterval, stoppingToken);
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var uow   = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var now     = DateTimeOffset.UtcNow;
        var spec    = new ExpiredPendingSwapRequestsSpecification(now);
        var expired = await uow.Repository<SlotSwapRequest>().GetAsync(spec, ct);

        if (expired.Count == 0)
            return;

        _logger.LogInformation(
            "SwapRequestExpiryService: expiring {Count} swap request(s).", expired.Count);

        var swapRepo = uow.Repository<SlotSwapRequest>();

        foreach (var request in expired)
        {
            request.Status = SlotSwapStatus.Expired;
            swapRepo.Update(request);

            var requesterUser = await uow.Repository<User>()
                .GetByIdAsync(request.RequesterPatient.UserId, ct);

            if (requesterUser is not null)
                await email.SendAsync(
                    requesterUser.Email,
                    "Your slot swap request has expired",
                    "Your slot swap offer was not responded to within 24 hours and has expired. " +
                    "You may submit a new swap request if you still wish to change your appointment time.",
                    ct);
        }

        await uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "SwapRequestExpiryService: {Count} swap request(s) marked Expired.", expired.Count);
    }
}
