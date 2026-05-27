using MediatR;

namespace HealthPlatform.Application.Features.Admin;

public sealed record UnlockUserCommand(Guid UserId) : IRequest<UnlockUserResult>;

public sealed record UnlockUserResult(bool IsSuccess, string? Error);
