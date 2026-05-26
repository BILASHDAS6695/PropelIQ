namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Abstracts email delivery so the Application layer remains transport-agnostic.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends a single email message.
    /// </summary>
    /// <param name="toAddress">Recipient email address.</param>
    /// <param name="subject">Email subject line.</param>
    /// <param name="body">Plain-text or HTML body of the email.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendAsync(string toAddress, string subject, string body, CancellationToken ct = default);
}
