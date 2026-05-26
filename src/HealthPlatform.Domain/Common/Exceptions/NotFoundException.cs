namespace HealthPlatform.Domain.Common.Exceptions;

/// <summary>Thrown when a requested aggregate root is not found.</summary>
public sealed class NotFoundException : DomainException
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} with key '{key}' was not found.") { }
}
