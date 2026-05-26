using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Application.Features.Admin;

internal sealed class DeactivateUserCommandHandler
    : IRequestHandler<DeactivateUserCommand, DeactivateUserResult>
{
    private readonly IUnitOfWork _uow;
    private readonly ISessionStore _sessionStore;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<DeactivateUserCommandHandler> _logger;

    public DeactivateUserCommandHandler(
        IUnitOfWork uow,
        ISessionStore sessionStore,
        IJwtTokenService jwtTokenService,
        ILogger<DeactivateUserCommandHandler> logger)
    {
        _uow = uow;
        _sessionStore = sessionStore;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task<DeactivateUserResult> Handle(
        DeactivateUserCommand request,
        CancellationToken cancellationToken)
    {
        var userRepo = _uow.Repository<User>();
        var user = await userRepo.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
            return new DeactivateUserResult(false, "User not found.");

        if (user.IsActive)
        {
            user.IsActive = false;
            userRepo.Update(user);
            await _uow.SaveChangesAsync(cancellationToken);
        }

        // Revoke auth artifacts immediately (idempotent if missing).
        await _sessionStore.DeleteSessionAsync(request.UserId, cancellationToken);
        await _jwtTokenService.RevokeRefreshTokenAsync(request.UserId, cancellationToken);

        _logger.LogInformation("User {UserId} deactivated and auth artifacts revoked", request.UserId);
        return new DeactivateUserResult(true, null);
    }
}
