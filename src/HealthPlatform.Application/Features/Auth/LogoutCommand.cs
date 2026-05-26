using MediatR;

namespace HealthPlatform.Application.Features.Auth;

public sealed record LogoutCommand(Guid UserId) : IRequest<LogoutResult>;

public sealed record LogoutResult(bool IsSuccess);
