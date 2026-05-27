namespace HealthPlatform.Domain.Common.Exceptions;

/// <summary>
/// Thrown when an authenticated user attempts to access a resource or operation
/// that their role or ownership does not permit.
/// Maps to HTTP 403 Forbidden.
/// </summary>
public sealed class ForbiddenAccessException : DomainException
{
    public ForbiddenAccessException()
        : base("You do not have permission to perform this action.") { }

    public ForbiddenAccessException(string message)
        : base(message) { }
}
