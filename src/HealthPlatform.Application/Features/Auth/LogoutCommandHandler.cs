using HealthPlatform.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Application.Features.Auth;

internal sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, LogoutResult>
{
    private readonly ISessionStore _sessionStore;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<LogoutCommandHandler> _logger;

    public LogoutCommandHandler(
        ISessionStore sessionStore,
        IJwtTokenService jwtTokenService,
        ILogger<LogoutCommandHandler> logger)
    {
        _sessionStore = sessionStore;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task<LogoutResult> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        // Idempotent revocation: deleting missing keys is treated as success.
        await _sessionStore.DeleteSessionAsync(request.UserId, cancellationToken);
        await _jwtTokenService.RevokeRefreshTokenAsync(request.UserId, cancellationToken);

        _logger.LogInformation("Logout succeeded for user {UserId}", request.UserId);
        return new LogoutResult(true);
    }
}
