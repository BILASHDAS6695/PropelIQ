using MediatR;

namespace HealthPlatform.Application.Features.Auth;

public sealed record RefreshTokenCommand(
    Guid   UserId,
    string RefreshToken) : IRequest<RefreshTokenResult>;

public sealed record RefreshTokenResult(
    bool    IsSuccess,
    string? AccessToken,
    string? RefreshToken,
    int     ExpiresIn,
    string? Error);
