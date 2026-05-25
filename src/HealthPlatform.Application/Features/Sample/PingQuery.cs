using MediatR;

namespace HealthPlatform.Application.Features.Sample;

public sealed record PingQuery : IRequest<string>;

public sealed class PingQueryHandler : IRequestHandler<PingQuery, string>
{
    public Task<string> Handle(PingQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult("Pong");
    }
}
