using MediatR;

namespace HealthPlatform.Application.Features.Admin;

public sealed record DeactivateUserCommand(Guid UserId) : IRequest<DeactivateUserResult>;

public sealed record DeactivateUserResult(bool IsSuccess, string? Error);
