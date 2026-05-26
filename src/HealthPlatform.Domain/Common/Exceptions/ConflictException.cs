namespace HealthPlatform.Domain.Common.Exceptions;

/// <summary>Thrown when an operation would create a conflicting state.</summary>
public sealed class ConflictException : DomainException
{
    public ConflictException(string message) : base(message) { }
}
