using HealthPlatform.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Infrastructure.Messaging;

/// <summary>
/// Development-phase email sender that logs the message instead of
/// transmitting it.  Replace with a real SMTP / SendGrid implementation
/// when email delivery is in scope.
/// </summary>
internal sealed class NoOpEmailSender : IEmailSender
{
    private readonly ILogger<NoOpEmailSender> _logger;

    public NoOpEmailSender(ILogger<NoOpEmailSender> logger) => _logger = logger;

    public Task SendAsync(
        string toAddress,
        string subject,
        string body,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[NoOpEmailSender] To={ToAddress} Subject={Subject} Body={Body}",
            toAddress, subject, body);

        return Task.CompletedTask;
    }
}
