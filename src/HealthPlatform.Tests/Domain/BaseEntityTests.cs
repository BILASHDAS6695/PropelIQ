using HealthPlatform.Domain.Common;

namespace HealthPlatform.Tests.Domain;

public class BaseEntityTests
{
    private sealed class TestEntity : AuditableEntity { }

    [Fact]
    public void AuditableEntity_InheritsBaseEntity_HasIdProperty()
    {
        // Arrange & Act
        var entity = new TestEntity { Id = Guid.NewGuid() };

        // Assert
        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.IsAssignableFrom<BaseEntity>(entity);
    }

    [Fact]
    public void AuditableEntity_HasAuditProperties()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

        // Act
        var entity = new TestEntity
        {
            CreatedAt = now,
            UpdatedAt = now
        };

        // Assert
        Assert.Equal(now, entity.CreatedAt);
        Assert.Equal(now, entity.UpdatedAt);
    }
}
