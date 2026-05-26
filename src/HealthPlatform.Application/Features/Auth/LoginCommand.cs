using MediatR;

namespace HealthPlatform.Application.Features.Auth;

public sealed record LoginCommand(
    string Email,
    string Password) : IRequest<LoginResult>;

public sealed record LoginResult(
    bool    IsSuccess,
    string? AccessToken,
    string? RefreshToken,
    int     ExpiresIn,
    string? Error);
