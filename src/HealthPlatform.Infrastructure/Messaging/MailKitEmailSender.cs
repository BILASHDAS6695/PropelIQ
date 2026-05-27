using HealthPlatform.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace HealthPlatform.Infrastructure.Messaging;

/// <summary>
/// Sends email via SMTP using MailKit.
/// Validates the recipient address before attempting delivery.
/// Falls back to plain-text body detection; logs permanently-failed deliveries.
/// </summary>
internal sealed class MailKitEmailSender : IEmailSender
{
    private readonly SmtpSettings               _settings;
    private readonly ILogger<MailKitEmailSender> _logger;

    public MailKitEmailSender(
        IOptions<SmtpSettings>           settings,
        ILogger<MailKitEmailSender> logger)
    {
        _settings = settings.Value;
        _logger   = logger;
    }

    public async Task SendAsync(
        string toAddress,
        string subject,
        string body,
        CancellationToken ct = default)
    {
        // Guard — invalid address: log warning and skip; never block the caller.
        if (!IsValidEmail(toAddress))
        {
            _logger.LogWarning(
                "MailKitEmailSender: invalid recipient address '{ToAddress}' — skipping.",
                toAddress);
            return;
        }

        var message = BuildMessage(toAddress, subject, body);

        using var client = new SmtpClient();

        try
        {
            await client.ConnectAsync(
                _settings.Host,
                _settings.Port,
                _settings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
                ct);

            if (!string.IsNullOrWhiteSpace(_settings.UserName))
                await client.AuthenticateAsync(_settings.UserName, _settings.Password, ct);

            await client.SendAsync(message, ct);
        }
        catch (Exception ex)
        {
            // Permanently-failed delivery logged here.
            // Hangfire retry policy (Task 002) handles transient failures
            // before the job is marked as permanently failed.
            _logger.LogError(
                ex,
                "MailKitEmailSender: permanent delivery failure to '{ToAddress}' Subject='{Subject}'.",
                toAddress,
                subject);
            throw;
        }
        finally
        {
            if (client.IsConnected)
                await client.DisconnectAsync(quit: true, ct);
        }
    }

    private MimeMessage BuildMessage(string toAddress, string subject, string body)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(toAddress));
        message.Subject = subject;

        // Detect HTML vs plain-text by checking the first non-whitespace character.
        var isHtml = body.TrimStart().StartsWith('<');
        message.Body = new TextPart(isHtml ? TextFormat.Html : TextFormat.Plain)
        {
            Text = body,
        };

        return message;
    }

    private static bool IsValidEmail(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return false;

        // Require an @ sign before delegating to MailKit — MailKit's parser
        // accepts bare hostnames (e.g. "not-an-email") as local-domain addresses,
        // which would pass RFC 5321 parsing but fail at SMTP delivery.
        if (!address.Contains('@'))
            return false;

        try
        {
            // Delegate RFC 5321 validation to MailKit's own parser.
            var mailbox = MailboxAddress.Parse(address);
            return mailbox is not null;
        }
        catch
        {
            return false;
        }
    }
}
