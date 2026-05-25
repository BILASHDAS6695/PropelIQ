using HealthPlatform.Application;
using HealthPlatform.Application.Features.Sample;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace HealthPlatform.Tests.Application;

public class PingQueryTests
{
    [Fact]
    public async Task PingQuery_ReturnsPong()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(new PingQuery());

        // Assert
        Assert.Equal("Pong", result);
    }
}
