using Hangfire;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Infrastructure.Messaging;

/// <summary>
/// Hangfire background job that delivers a single email via MailKit.
/// Retried up to 3 times with exponential back-off (5 min, 25 min, 125 min)
/// before being permanently failed and logged.
/// </summary>
internal sealed class SendEmailJob
{
    private readonly MailKitEmailSender    _sender;
    private readonly ILogger<SendEmailJob> _logger;

    public SendEmailJob(
        MailKitEmailSender    sender,
        ILogger<SendEmailJob> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 300, 1500, 7500 })]
    public async Task ExecuteAsync(string toAddress, string subject, string body)
    {
        _logger.LogInformation(
            "SendEmailJob: delivering email to '{ToAddress}' Subject='{Subject}'.",
            toAddress,
            subject);

        await _sender.SendAsync(toAddress, subject, body);
    }
}
