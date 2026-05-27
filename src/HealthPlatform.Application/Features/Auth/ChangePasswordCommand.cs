using MediatR;

namespace HealthPlatform.Application.Features.Auth;

public sealed record ChangePasswordCommand(
    Guid   UserId,
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword) : IRequest<ChangePasswordResult>;

public sealed record ChangePasswordResult(bool IsSuccess, string? Error);
