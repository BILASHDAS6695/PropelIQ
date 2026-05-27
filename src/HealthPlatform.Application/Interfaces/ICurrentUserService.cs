namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Provides the identity of the currently authenticated user.
/// Returns <c>null</c> for unauthenticated contexts (e.g., startup seeding,
/// background services) — callers must check <see cref="IsAuthenticated"/>
/// before consuming <see cref="UserId"/>.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId          { get; }
    bool  IsAuthenticated { get; }
}
